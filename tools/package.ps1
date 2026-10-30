# Copies exactly the two distributable DLLs from the Release build into dist/Jangseoru/
# and fails if anything else (e.g. a game or third-party assembly) would be shipped.
param(
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$pluginBin = Join-Path $root "src\Jangseoru.Plugin\bin\$Configuration\net472"
$dist = Join-Path $root 'dist\Jangseoru'
$expected = @('Jangseoru.Core.dll', 'Jangseoru.Plugin.dll')

foreach ($f in $expected) {
    if (-not (Test-Path -LiteralPath (Join-Path $pluginBin $f))) { throw "build output missing: $f (run dotnet build -c $Configuration first)" }
}

if (Test-Path -LiteralPath $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
foreach ($f in $expected) { Copy-Item -LiteralPath (Join-Path $pluginBin $f) -Destination $dist }

$actual = (Get-ChildItem -LiteralPath $dist -File | Select-Object -ExpandProperty Name | Sort-Object)
if (($actual -join '|') -ne (($expected | Sort-Object) -join '|')) {
    throw "unexpected dist contents: $($actual -join ', ')"
}
Write-Host "dist/Jangseoru: $($actual -join ', ')"
