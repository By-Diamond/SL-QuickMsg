# 构建（无需管理员）
$ErrorActionPreference = 'Stop'

$root = 'E:\OpenCode\Projects\SL-QuickMsg'
$dotnet = 'E:\OpenCode\Tools\dotnet\dotnet.exe'

Write-Host '[1/2] 构建托盘程序'
& $dotnet build "$root\src\SLQuickMsg.Tray" -c Release -v q
if ($LASTEXITCODE -ne 0) { throw '托盘构建失败' }

Write-Host '[2/2] 汇总到 build'
$source = "$root\src\SLQuickMsg.Tray\bin\Release\net8.0-windows"
New-Item -ItemType Directory -Force -Path "$root\build" | Out-Null
Get-Process SLQuickMsg.Tray -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Copy-Item "$source\SLQuickMsg.Tray.exe" "$root\build\" -Force
Copy-Item "$source\SLQuickMsg.Tray.dll" "$root\build\" -Force
Copy-Item "$source\SLQuickMsg.Tray.runtimeconfig.json" "$root\build\" -Force
Copy-Item "$source\SLQuickMsg.Tray.deps.json" "$root\build\" -Force

Get-ChildItem "$root\build" | Select-Object Name, Length, LastWriteTime
