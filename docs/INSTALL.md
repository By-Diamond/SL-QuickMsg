# 安装与排错

## 1. 运行要求

| 项 | 要求 |
|---|---|
| 系统 | Windows 10/11 x64 |
| 运行时 | .NET 8 Desktop Runtime（本机已有 `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.x`） |
| 游戏 | SCP:SL **无边框窗口**模式（独占全屏下覆盖层不可见） |
| 权限 | 与游戏同级；游戏若以管理员运行，本工具也要管理员 |

## 2. 运行

```powershell
E:\OpenCode\Projects\SL-QuickMsg\build\SLQuickMsg.Tray.exe
```

托盘出现即生效；右键菜单：

- **打开配置**：管理轮盘、槽位、触发键；界面里的「预览轮盘」按钮可直接看 3 秒效果
- **打开配置文件夹**：`build\config\SLQuickMsg.json`
- **退出**

## 3. 构建

```powershell
powershell -ExecutionPolicy Bypass -File E:\OpenCode\Projects\SL-QuickMsg\scripts\build.ps1
```

产物在 `build\`，配置在 `build\config\`。

## 4. 排错

| 现象 | 原因/处理 |
|---|---|
| 按 G 没反应 | 游戏不是前台；或触发键没配对（配置里 `key` 必须是 `G` 这类名字） |
| 轮盘出现但不动 | 原始输入没收到（多为权限不一致，试试管理员运行） |
| 轮盘看不见但能发 | 独占全屏；改无边框窗口 |
| 发的消息缺字/乱码 | `commandPrefix` 或正文含游戏不支持字符；看日志 `%LOCALAPPDATA%\SLQuickMsg\app.log` |
| 在桌面按 G 也被吞 | 不应该发生；检查是否有第二个实例，日志里有"轮盘按键"记录 |
| 游戏里 G 仍会丢物品 | 说明钩子没生效（被安全软件拦），看日志与托盘提示 |

日志：`%LOCALAPPDATA%\SLQuickMsg\app.log`

## 5. 卸载

删掉 `E:\OpenCode\Projects\SL-QuickMsg` 即可；游戏目录零改动。
