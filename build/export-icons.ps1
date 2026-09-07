# Regenerates the committed icon assets from the group icon the app draws for
# itself, so the icon on the executable can never drift from the one shown inside
# the windows:
#
#   src/ReProgman/ReProgman.ico    the Windows executable icon (ApplicationIcon)
#   src/ReProgman/ReProgman.icns   the icon of the macOS bundle
#
#   ./build/export-icons.ps1
#
# Both are committed rather than produced during a build: ApplicationIcon has to
# exist before the build that would produce it, and rendering needs a window
# server, which a build machine does not have. Run this after changing the
# artwork and commit the result.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$tool = Join-Path $root 'tools\ReProgman.IconExport\ReProgman.IconExport.csproj'
$ico = Join-Path $root 'src\ReProgman\ReProgman.ico'
$icns = Join-Path $root 'src\ReProgman\ReProgman.icns'

Write-Host 'Rendering the icon assets...'
dotnet run --project $tool -c Debug --nologo -- --ico $ico --icns $icns
if ($LASTEXITCODE -ne 0) { throw "icon export failed with exit code $LASTEXITCODE" }

Write-Host 'Done. Rebuild the project to embed the .ico into the executable.'
