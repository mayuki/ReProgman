<#
.SYNOPSIS
    Publishes ReProgman as a NativeAOT binary to artifacts/publish-aot.

.DESCRIPTION
    Loads the Visual Studio C++ build environment (vcvarsall) into this process
    and publishes with IlcUseEnvironmentalTools=true.

    Why not plain `dotnet publish`: the ILCompiler toolchain discovery captures
    the *console output* of vcvarsall.bat to locate link.exe. Visual Studio 2026
    (v18) writes warning noise to stderr during vcvarsall, which corrupts the
    captured linker path. Importing the environment ourselves and telling ILC to
    use the environment tools sidesteps the fragile capture entirely.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    # $PSScriptRoot is empty while parameter defaults are evaluated, so the
    # default output path is computed in the body instead.
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
if (-not $Output) {
    $Output = Join-Path $PSScriptRoot '..\artifacts\publish-aot'
}

if (-not (Get-Command link.exe -ErrorAction SilentlyContinue)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found. Install Visual Studio with the 'Desktop development with C++' workload."
    }

    $vsBase = & $vswhere -latest -prerelease -products * `
        -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -property installationPath
    if (-not $vsBase) {
        throw "No Visual Studio installation with the C++ toolset was found."
    }

    $vcvarsall = Join-Path $vsBase 'VC\Auxiliary\Build\vcvarsall.bat'
    $arch = if ($Runtime -match 'arm64') { 'amd64_arm64' } else { 'amd64' }
    & cmd /c "`"$vcvarsall`" $arch >nul 2>nul && set" | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
        }
    }

    if (-not (Get-Command link.exe -ErrorAction SilentlyContinue)) {
        throw "link.exe is still not on PATH after running vcvarsall.bat."
    }
}

dotnet publish "$PSScriptRoot\..\src\ReProgman" -c $Configuration -r $Runtime -o $Output -p:IlcUseEnvironmentalTools=true
exit $LASTEXITCODE
