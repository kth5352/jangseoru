# Reverses tools/install-test-env.ps1 on a game folder that was vanilla before: removes winhttp.dll,
# doorstop_config.ini and the whole BepInEx folder (everything in it was created by the install or by BepInEx
# itself on first run). The BepInEx log is copied out first so the evidence is not lost.
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\LegendOfMortal',
    [string]$EvidenceDir = (Join-Path $PSScriptRoot '..\..\backups')
)
$ErrorActionPreference = 'Stop'

if (Get-Process -Name Mortal -ErrorAction SilentlyContinue) { throw 'The game is running. Close it first.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mortal.exe'))) { throw "Not a game folder: $GameDir" }

$log = Join-Path $GameDir 'BepInEx\LogOutput.log'
if (Test-Path -LiteralPath $log) {
    New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
    Copy-Item -LiteralPath $log -Destination (Join-Path $EvidenceDir ("LogOutput_{0}.log" -f (Get-Date -Format 'yyyyMMdd_HHmmss')))
}
foreach ($n in 'winhttp.dll', 'doorstop_config.ini') {
    $p = Join-Path $GameDir $n
    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
}
$b = Join-Path $GameDir 'BepInEx'
if (Test-Path -LiteralPath $b) { Remove-Item -LiteralPath $b -Recurse -Force }
Write-Host 'test environment removed; game folder is vanilla again'
