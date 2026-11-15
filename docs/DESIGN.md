# 设计说明

## 1. 需求

- 按住触发键（默认 `G`）→ 轮盘出现
- 鼠标推向某个方向 → 该方向消息高亮；中间死区 = 不选
- 松开 → 自动 `~`、`.tc 正文`、回车、`Esc`
- 轮盘槽位数可配（2–8），多个触发键各对应一个独立轮盘
- 配置界面：标题（轮盘显示）+ 正文（实际发送）

## 2. 三个难点与解法

| 难点 | 现象 | 解法 |
|---|---|---|
| 游戏抢焦点 | 正常置顶窗口会激活自己，游戏失焦、键盘输入跑到我们窗口 | `WS_EX_NOACTIVATE` + `SW_SHOWNOACTIVATE` + `WS_EX_TRANSPARENT`，从不调用 `SetForegroundWindow` |
| 游戏锁光标 | Unity 每帧把光标拉回窗口中心，`GetCursorPos` 读到的是中心，方向判断失效；`WH_MOUSE_LL` 吞事件也拦不住 raw input | **轮盘主动接管前台**（`AttachThreadInput` + `SetForegroundWindow`）：游戏失焦后不再读取鼠标；`WM_INPUT` 相对位移作为抢焦点失败时的兜底 |
| 触发键冲突 | `G` 在游戏里是丢弃物品 | 低级键盘钩子（`WH_KEYBOARD_LL`）吞掉触发键，游戏完全收不到；且只在 SCPSL 前台时拦截 |

## 3. 数据流

```
KeyboardHook (WH_KEYBOARD_LL)
   │ keydown(vk) / keyup(vk)
   ▼
WheelEngine ── timer 25ms ──▶ RawMouse.DeltaX/DeltaY
   │                            │
   │ 角度 → 槽位下标              │
   ▼                            ▼
WheelOverlay (UpdateLayeredWindow, 逐像素透明)  高亮扇区
   │
   │ keyup + 选中
   ▼
GameSend.SendCommand(".tc 正文")
   ~ ─30ms─ Unicode 文本 ─18ms─ Enter ─18ms─ Esc
```

## 4. 角度到槽位

槽位 0 在正上方，顺时针排列，可整体旋转 `rotationDegrees`：

```
step = 360 / count
deg  = atan2(-dy, dx)            // 屏幕坐标 y 向下，取反后 90 = 正上
idx  = round((90 - (deg - rotation)) / step) mod count
```

- `rotationDegrees = 0` → 上 / 右 / 下 / 左
- `rotationDegrees = 45` → 左上 / 右上 / 右下 / 左下

死区：累计位移长度 < 38 px 视为"中间不选"。

## 5. 视觉

- 逐像素 alpha 分层窗口（`UpdateLayeredWindow`），无黑边、无窗口阴影
- 扇区：半透明深色；选中：蓝色高亮 + 白边
- 中心圆：`松开发送` / `中间取消`
- 标题用 `Microsoft YaHei UI` 抗锯齿渲染，超长省略

## 6. 发送时序

| 步骤 | 按键 | 等待 |
|---|---|---|
| 打开控制台 | `VK_OEM_3`（`~`） | 30 ms |
| 输入命令 | `SendInput` Unicode（整段一次发） | 18 ms |
| 提交 | `Enter` | 18 ms |
| 关闭控制台 | `Esc` | — |

文本走 `KEYEVENTF_UNICODE`，不经过剪贴板，也不会触发输入法状态问题。

## 7. 安全边界

- 不读游戏内存、不注入、不写游戏目录
- 钩子只做两件事：读取按键状态、可选地吞掉鼠标
- 只对 `SCPSL` 前台时生效

## 8. 后续可做

- 槽位图标/颜色
- 轮盘内层显示正文预览
- 发送时目标服务器过滤（例如只在装了 TextChatMeow 的服启用）
- 语音/音效反馈
