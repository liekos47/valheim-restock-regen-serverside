# Renders icon.svg to the 256x256 icon.png in the repo root (the Thunderstore icon) with headless Edge.
# Pass a path to render somewhere else.
param([string]$Target = (Join-Path (Split-Path $PSScriptRoot -Parent) "icon.png"))

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "msedge.exe not found" }
$profileDir = Join-Path $env:TEMP "restockregen-icon-render"
$url = "file:///" + ((Join-Path $PSScriptRoot "icon.svg") -replace '\\', '/')

& $edge --headless --disable-gpu --hide-scrollbars --user-data-dir="$profileDir" --default-background-color=00000000 `
	--window-size="256,256" --screenshot="$Target" $url 2>$null | Out-Null
