[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

function Test-DotnetSdk {
    try { return ((dotnet --list-sdks) -match '^8\.').Count -gt 0 } catch { return $false }
}

if (-not (Test-DotnetSdk)) {
    Write-Host 'Installing .NET 8 SDK…'
    winget install --id Microsoft.DotNet.SDK.8 --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity
}
if (-not (Test-DotnetSdk)) {
    throw 'The .NET 8 SDK is still unavailable. Restart Windows once, then run this script again.'
}

$pythonCommand = Get-Command py -ErrorAction SilentlyContinue
if ($pythonCommand) {
    $python = @('py', '-3.12')
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $python = @('python')
} else {
    throw 'Python 3.12 was not found. Install Python 3.12 x64, then run this script again.'
}

$venvPython = Join-Path $projectRoot '.venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $venvPython)) {
    Write-Host 'Creating the private Python environment…'
    if ($python.Count -gt 1) {
        & $python[0] $python[1] -m venv (Join-Path $projectRoot '.venv')
    } else {
        & $python[0] -m venv (Join-Path $projectRoot '.venv')
    }
}

Write-Host 'Installing local OCR dependencies…'
& $venvPython -m pip install --upgrade pip
& $venvPython -m pip install paddlepaddle==3.2.0 -i https://www.paddlepaddle.org.cn/packages/stable/cpu/
& $venvPython -m pip install -r (Join-Path $projectRoot 'ocr\requirements.txt')

Write-Host 'Restoring and building QQ Jev HUD…'
dotnet restore (Join-Path $projectRoot 'QQJevHud.sln')
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
dotnet build (Join-Path $projectRoot 'QQJevHud.sln') --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
Write-Host 'Ready. Run start.cmd to launch QQ Jev HUD.' -ForegroundColor Green
