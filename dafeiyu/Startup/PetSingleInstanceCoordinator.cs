using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace dafeiyu.Startup;

/// <summary>
/// 桌宠单实例 + 激活协调器。复用主程序 Widget 的「Mutex + 命名管道」思路：
/// 用命名互斥锁判断是否为主实例，用命名管道向已运行实例发送「唤起」消息。
/// 独立骨架内自带一份精简实现，不依赖主程序代码。
/// </summary>
internal static class PetInstanceIdentity
{
    public const string MutexName = @"Local\dafeiyu.Demo";
    public const string PipeName = "dafeiyu.Demo.Activation";
    public const string ActivationMessage = "activate";

    public static string GetPipeName(string profileScope)
    {
        var input = string.Concat("dafeiyuInstance.v1", '\0', profileScope);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var instanceHash = Convert.ToHexString(digest).ToLowerInvariant()[..32];
        return $"dafeiyu.Activation.{profileScope}.{instanceHash}";
    }
}

internal sealed class PetSingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _listenerGate = new();
    private readonly int _mutexOwnerThreadId;
    private bool _ownsMutex;
    private Task? _listenerTask;
    private NamedPipeServerStream? _activeServer;
    private bool _disposed;

    public PetSingleInstanceCoordinator(string mutexName = PetInstanceIdentity.MutexName)
    {
        _mutex = new Mutex(initiallyOwned: false, mutexName);
        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        _mutexOwnerThreadId = _ownsMutex ? Environment.CurrentManagedThreadId : -1;
    }

    public bool IsPrimary => _ownsMutex;

    public void StartListener(Action activatePrimaryWindow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary)
        {
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        }

        _listenerTask = ListenForActivationAsync(activatePrimaryWindow, _cancellation.Token);
    }

    public bool TryActivateExisting()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPrimary)
        {
            return false;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".", PetInstanceIdentity.PipeName, PipeDirection.Out);
                client.Connect(250);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(PetInstanceIdentity.ActivationMessage);
                return true;
            }
            catch (IOException)
            {
                if (attempt == 2)
                {
                    return false;
                }

                Thread.Sleep(50);
            }
            catch (TimeoutException)
            {
                if (attempt == 2)
                {
                    return false;
                }

                Thread.Sleep(50);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        lock (_listenerGate)
        {
            _activeServer?.Dispose();
        }

        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        if (_ownsMutex && Environment.CurrentManagedThreadId == _mutexOwnerThreadId)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private async Task ListenForActivationAsync(Action activatePrimaryWindow, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var server = new NamedPipeServerStream(
                    PetInstanceIdentity.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                lock (_listenerGate)
                {
                    _activeServer = server;
                }

                try
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    using var reader = new StreamReader(server);
                    if (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ==
                        PetInstanceIdentity.ActivationMessage)
                    {
                        activatePrimaryWindow();
                    }
                }
                finally
                {
                    lock (_listenerGate)
                    {
                        if (ReferenceEquals(_activeServer, server))
                        {
                            _activeServer = null;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
