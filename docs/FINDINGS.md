# 实测记录

## 2026-11-15 · 注入可行性

| 步骤 | 结果 |
|---|---|
| 游戏目录新增 `winhttp.dll`（BepInEx） | SL-AC 弹窗 `Unexpected game file found (winhttp.dll)`，阻止启动 |
| 清理全部新增文件 | 目录恢复原状（22 项） |
| 注入器 + payload 注入 `notepad.exe` | **成功**（模块基址 0xA8600000）→ 注入器与 payload 本身可用 |
| 注入器 + payload 注入 `SCPSL.exe` | `LoadLibraryW` 返回 0，**游戏拒绝加载外来 DLL**；`%TEMP%\slqm_probe.log` 未生成 |

结论：

- 注入链路技术上是通的（notepad 验证通过），失败点在游戏进程侧。
- 游戏目录零改动仍被拦截 → 说明拦截发生在**运行时模块加载**，不是文件比对。
- 继续推进只能依赖"绕过模块加载拦截"的手段（手工映射、取消 LoadLibrary 钩子、隐藏模块等），
  属于对抗反作弊，本项目不做。

## 可用的外部事实

- `GameAssembly.dll` 导出完整 `il2cpp_*`（`objdump -p` 已核实）→ 若将来拦截解除，原生 payload 方案可直接用。
- 客户端控制台入口在 `GameAssembly` 元数据中存在：`TypeCommand`、`GameConsoleTransmission.SendToServer`。

## 2026-11-15 · 方案切换

注入两条路都被反作弊封死（文件比对 + 运行时模块加载），继续只能做对抗反作弊，故放弃注入。
改为纯外部方案：全局钩子 + 轮盘覆盖层 + `SendInput` 模拟 `~ .tc 正文 回车 Esc`。

已清理：

- 游戏目录：无任何残留（20 项原始文件）
- `E:\OpenCode\Tools\mingw64`（原生编译工具链）
- `E:\OpenCode\Temp\dl`、`dumper`、`il2cpp-dump`、`bep.zip`、安装脚本
- 项目内 `native/`、`src/SLQuickMsg.Injector/`、旧 `build/` 产物
