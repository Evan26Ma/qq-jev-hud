# 在桌面创建「QQ Jev HUD」快捷方式。
# 需要一个已经构建好的 exe；先运行 start.cmd 或 dotnet build 即可。
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $projectRoot 'src\QQJevHud\bin\Debug\net8.0-windows10.0.19041.0\QQJevHud.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host '找不到已构建的程序，正在构建…'
    dotnet build (Join-Path $projectRoot 'QQJevHud.sln') --nologo | Out-Null
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "构建后仍找不到 $exe，请先运行 setup.ps1。"
}

$link = Join-Path ([Environment]::GetFolderPath('Desktop')) 'QQ Jev HUD.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($link)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $projectRoot
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'QQ Jev HUD 无侵入聊天意图 / 风险悬浮卡'
$shortcut.Save()

Write-Host "已创建桌面快捷方式：$link" -ForegroundColor Green
Write-Host "指向：$exe"
