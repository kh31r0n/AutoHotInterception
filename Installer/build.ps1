<#
.SYNOPSIS
    Builds the C# solution, runs the unit tests and compiles the installer to Installer\Output\AHI-Setup-<Version>.exe.
.DESCRIPTION
    Needs the .NET SDK (8 or later), Inno Setup 6.5 or later, and interception.dll in C#\Dependencies\x86 and x64
    (see C#\Dependencies\Readme.md). Tests marked Category=AutoHotkey run only if AutoHotkey v2 is installed.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Installer\build.ps1 -Version 0.10.0
#>
param(
    [string]$Version = "0.10.0",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "C#\AutoHotInterception.sln"

foreach ($arch in "x86", "x64") {
    if (-not (Test-Path (Join-Path $root "C#\Dependencies\$arch\interception.dll"))) {
        throw "Missing C#\Dependencies\$arch\interception.dll - copy it from the Interception release zip (library folder)."
    }
}

if ($SkipTests) {
    & dotnet build $solution -c Release "-p:Version=$Version"
} else {
    # dotnet test builds the whole solution first
    & dotnet test $solution -c Release "-p:Version=$Version"
}
if ($LASTEXITCODE -ne 0) { throw "Build or tests failed." }

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php" }

& $iscc "/DAppVersion=$Version" (Join-Path $PSScriptRoot "AutoHotInterception.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed." }

Write-Host "Built $(Join-Path $PSScriptRoot "Output\AHI-Setup-$Version.exe")"
