# SL QuickMsg

**版本 0.1.0 · By Diamond**

- GitHub：[github.com/By-Diamond/SL-QuickMsg](https://github.com/By-Diamond/SL-QuickMsg)
- Bilibili：[space.bilibili.com/602152864](https://space.bilibili.com/602152864)

SCP: Secret Laboratory 的快速聊天轮盘：按住一个键 → 轮盘出现 → 鼠标选方向 → 松手自动发 `.tc 正文`。

## 状态

- v1 已可运行：托盘常驻、轮盘、全局按键、自动发送
- 目标环境：SCP:SL 14.2.9 / Windows x64 / **无边框窗口**模式
- 方案：**纯外部**，不注入、不读内存、不往游戏目录写文件

## 使用

1. 运行 `build\SLQuickMsg.Tray.exe`（托盘出现）
2. 确保游戏是**无边框窗口**模式（独占全屏下任何第三方窗口都盖不上去）
3. 游戏内按住 `G` → 轮盘出现 → 鼠标推向某个方向 → 松开
4. 自动执行：`~` → `.tc 正文` → 回车 → `Esc`（约 70 ms）

托盘右键：打开配置 / 打开配置文件夹 / 退出。轮盘预览在配置界面里（「预览轮盘」按钮）。

## 配置

`build\config\SLQuickMsg.json`（首次运行自动生成；配置界面保存后热生效）

```json
{
  "commandPrefix": ".tc ",
  "notifyOnSend": false,
  "wheels": [
    {
      "key": "G",
      "name": "战术",
      "rotationDegrees": 0,
      "slots": [
        { "title": "集合", "body": "所有人到检查点集合" },
        { "title": "撤离", "body": "准备撤离，跟我走" }
      ]
    }
  ]
}
```

| 字段 | 说明 |
|---|---|
| `key` | 触发键，Unity KeyCode 名称：`G`、`F1`、`Keypad5`… |
| `rotationDegrees` | 0 = 槽位 0 在正上方（上/右/下/左）；45 = 斜角（左上/右上/右下/左下） |
| `slots` | 2–8 个；按顺时针排列；`title` 显示在轮盘上，`body` 是实际发送的正文 |

## 工作方式

```
全局键盘钩子（低级 Hook，只在 SCPSL 前台时拦截触发键）
  → 轮盘窗口主动接管前台（游戏失焦 = 停止读取鼠标，镜头不再转）
  → 光标相对轮盘中心的方向即选中方向（原始输入做兜底）
  → 松开 → 隐藏轮盘、把前台还给游戏 → SendInput：~ → Unicode 文本 → 回车 → Esc
```

关键技术点：

- **停止转视角**：轮盘打开时用 `AttachThreadInput + SetForegroundWindow` 接管前台，游戏失焦后不再读鼠标
- **光标被锁的兜底**：抢焦点失败时退回 `WM_INPUT` 原始输入的相对位移
- **按键不被游戏吃掉/吃掉游戏按键**：低级键盘钩子直接吞掉触发键，游戏收不到 `G`

## 已知限制

- 独占全屏无法覆盖轮盘，必须用无边框窗口
- 若游戏以管理员权限运行，托盘程序也需要管理员权限才能拦截按键
- 轮盘打开期间游戏会短暂失焦（可能静音/暂停渲染），松开后自动恢复
- 触发键只在 SCPSL 前台时被拦截，桌面/别的程序不受影响

## 目录

```
src/SLQuickMsg.Tray/          托盘 + 配置 + 钩子 + 轮盘
scripts/build.ps1             构建脚本
docs/DESIGN.md                设计
docs/INSTALL.md               安装与排错
docs/FINDINGS.md              历史实测（注入方案为何放弃）
```

## 历史

先用 BepInEx 注入，被 SL-AC 拦截（`winhttp.dll` 文件比对 + 运行时拒绝加载外来 DLL），
已完全清理并改用本方案，详见 `docs/FINDINGS.md`。
