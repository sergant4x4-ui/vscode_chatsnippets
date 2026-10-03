# Собирает установщик: installer\Output\ClaudeCodeChatSnippets-Setup-<версия>.exe
# Нужен Inno Setup 6: путь к ISCC.exe можно передать параметром или он берётся из стандартных мест.
param([string]$Iscc)

$root = Split-Path $PSScriptRoot -Parent
if (-not $Iscc) {
    $Iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc) { throw "ISCC.exe не найден. Установите Inno Setup 6 (jrsoftware.org) или передайте -Iscc <путь>." }

Get-Process ChatSnippets -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$PSScriptRoot\publish" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root\src\ChatSnippets.App" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -o "$PSScriptRoot\publish"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish не удался" }

& $Iscc "$PSScriptRoot\ChatSnippets.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC не удался" }
