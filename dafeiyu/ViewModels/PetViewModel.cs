namespace dafeiyu.ViewModels;

/// <summary>桌宠视觉状态。</summary>
public enum PetBodyState
{
    Idle,
    Alert,
    Expanded,
}

/// <summary>一条用于演示的 Mock 提醒。</summary>
public sealed record MockReminder(
    string Title,
    string Body,
    string PurposeLabel,
    string PriorityLabel,
    bool IsPinned);

/// <summary>
/// 桌宠状态与提醒数据。骨架阶段用 Mock 数据；未来接入宿主应用时，
/// 该数据由 <c>IReminderFeed / IReminderQueryService</c> 提供（见 docs/06_INTERFACE_STUBS.md）。
/// </summary>
public sealed class PetViewModel : ObservableObject
{
    private PetBodyState _bodyState = PetBodyState.Idle;
    private bool _isCollapsed;
    private MockReminder? _currentReminder;

    /// <summary>当前是否展开面板（可选）。骨架阶段仅用于示意。</summary>
    public PetBodyState BodyState
    {
        get => _bodyState;
        private set
        {
            if (SetProperty(ref _bodyState, value))
            {
                OnPropertyChanged(nameof(IsAlert));
                OnPropertyChanged(nameof(HasReminder));
            }
        }
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetProperty(ref _isCollapsed, value);
    }

    public MockReminder? CurrentReminder
    {
        get => _currentReminder;
        private set
        {
            if (SetProperty(ref _currentReminder, value))
            {
                OnPropertyChanged(nameof(HasReminder));
                OnPropertyChanged(nameof(ReminderTitle));
                OnPropertyChanged(nameof(ReminderBody));
                OnPropertyChanged(nameof(ReminderPurposeLabel));
                OnPropertyChanged(nameof(ReminderPriorityLabel));
                OnPropertyChanged(nameof(ReminderMessage));
            }
        }
    }

    public bool HasReminder => CurrentReminder is not null;
    public bool IsAlert => BodyState == PetBodyState.Alert;

    public string ReminderTitle => CurrentReminder?.Title ?? string.Empty;
    public string ReminderBody => CurrentReminder?.Body ?? string.Empty;
    public string ReminderPurposeLabel => CurrentReminder?.PurposeLabel ?? string.Empty;
    public string ReminderPriorityLabel => CurrentReminder?.PriorityLabel ?? string.Empty;

    /// <summary>气泡上的一行提示文案。</summary>
    public string ReminderMessage =>
        CurrentReminder is { } r
            ? $"{r.PurposeLabel} · {r.PriorityLabel}{(r.IsPinned ? " · 置顶" : string.Empty)}"
            : string.Empty;

    /// <summary>触发一条 Mock 提醒，进入提醒态。</summary>
    public void RaiseMockReminder(MockReminder reminder)
    {
        CurrentReminder = reminder;
        BodyState = PetBodyState.Alert;
    }

    /// <summary>完成/忽略后回到待机态，清空提醒。</summary>
    public void ClearReminder()
    {
        CurrentReminder = null;
        BodyState = PetBodyState.Idle;
    }

    /// <summary>用户主动收纳到托盘。</summary>
    public void CollapseToTray()
    {
        IsCollapsed = true;
    }

    /// <summary>从托盘恢复。</summary>
    public void RestoreFromTray()
    {
        IsCollapsed = false;
    }
}
