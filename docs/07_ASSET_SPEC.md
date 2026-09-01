# 07 · 精灵图序列帧资产规格

> 桌宠角色动画采用**精灵图序列帧（SpriteSheet）** + WPF `Storyboard`/定时器推进。
> 本文定义资产尺寸、命名、帧序、元数据与资源放置路径，供美术与对接使用。

## 1. 目录约定

```text
src/windows/ReminNote.Pet/Assets/
  Sprite/
    pet_base.png          # 静态底图（可选，用于待机首帧/未加载帧）
    pet_idle.png          # 待机动画精灵图
    pet_alert.png         # 提醒动画精灵图
    pet_tap.png           # 点按/交互动画精灵图
  Meta/
    pet_idle.json         # 帧元数据（尺寸/帧序/帧率/循环）
    pet_alert.json
    pet_tap.json
```

> 若未来支持多角色/主题，可在 `Meta/` 增加 `theme_<name>.json` 选择集，用资源字典键驱动。

## 2. 尺寸与规格（建议值，可调整）

- 逻辑画布：`120 × 120 px`（DP），实际精灵图按此 **2x / 3x** 分辨率导出以兼容高 DPI（建议至少 240×240 源图）。
- 帧大小：**方形**，单帧 `120×120`；SpriteSheet 横向排列。
- 帧率：待机 `8 fps`（低功耗慢动作）；提醒 `12–15 fps`（更有冲击）；点按 `12 fps`。
- 循环：待机**循环**；提醒**播放一次后停留最后一帧**（或短暂循环直到用户处理）；点按播放一次。
- 透明背景：PNG-24 / 8-bit alpha，无边缘残留，无抖动。
- 尺寸需在 `Meta/*.json` 的 `frameSize` 中声明，运行时据此切片。

## 3. SpriteSheet 帧元数据 JSON

每张精灵图一个 JSON，命名与图一致。例如 `pet_idle.json`：

```json
{
  "image": "pet_idle.png",
  "frameSize": { "width": 120, "height": 120 },
  "columns": 4,
  "rows": 1,
  "frameCount": 4,
  "framesPerSecond": 8,
  "loop": true,
  "states": [
    { "name": "idle", "frames": [0, 1, 2, 3] }
  ]
}
```

字段说明：

| 字段 | 含义 |
|------|------|
| `frameSize` | 单帧宽高（DP） |
| `columns / rows` | 精灵图网格（本方案横向排列，`rows=1`） |
| `frameCount` | 总帧数 |
| `framesPerSecond` | 播放帧率 |
| `loop` | 是否循环 |
| `states[].frames` | 帧序号序列（可视化索引） |

## 4. 状态到资源映射

| 动画状态 | 触发 | 资源 | 表现 |
|----------|------|------|------|
| `idle` | 待机常态 | `pet_idle` | 循环慢动作，低功耗 |
| `alert` | 收到 UNREAD 提醒 | `pet_alert` | 播一次或短循环，附气泡 + 音效 |
| `tap` | 用户点按 | `pet_tap` | 播一次，随后进入面板/展开 |
| `sleep`（收纳态） | 用户收纳 | 可选：缩小/半透明 | 隐藏细节，保留托盘功能 |

## 5. 无障碍与降级

- 动画仅作辅助表达，**不应成为唯一信息载体**——提醒文字始终以 `Alert` 气泡 + `AutomationProperties.LiveSetting="Polite"` 表达。
- 尊重系统动效减少偏好（若检测到 `reduced animation`），回退为**静态帧 + 非动画气泡**。
- 高对比 / 减少透明模式下，回退为**不透明、可读**的背景与文字（对齐主程序 P0-07 无障碍门槛）。
- 图像必须包含`AccessibleFocusBrush` 焦点令牌等无障碍资源（见 `docs/08_WINDOW_UX.md`）。

## 6. 画质与性能

- 单帧越小越省电；待机可进一步降帧（可配置）。
- 建议对所有位图开启 `SnapsToDevicePixels` / `UseLayoutRounding`，避免高 DPI 模糊。
- 动画仅在**窗口可见且未收纳**时推进；收纳/最小化时暂停帧循环省资源。
- 无第三方图像库依赖——由 WPF `ImageSource` + `CroppedBitmap`/`Int32Rect` 切片即可。
