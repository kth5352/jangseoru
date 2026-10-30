# Extracts the BepInEx/HarmonyX reference DLLs from the repository's mod.zip into libs/.
# Read-only with respect to the game and any installed mods: it only writes inside mod/libs.
param(
    [string]$ZipPath = (Join-Path $PSScriptRoot '..\..\mod.zip'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\libs')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$wanted = @(
    'BepInEx/core/BepInEx.Unity.Mono.dll',
    'BepInEx/core/BepInEx.Core.dll',
    'BepInEx/core/0Harmony.dll'
)

if (-not (Test-Path -LiteralPath $ZipPath)) { throw "mod.zip not found: $ZipPath" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath))
try {
    foreach ($name in $wanted) {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq $name } | Select-Object -First 1
        if (-not $entry) { throw "entry missing from zip: $name" }
        $dest = Join-Path (Resolve-Path -LiteralPath $OutDir) $entry.Name
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
        Write-Host "extracted $($entry.Name)"
    }
}
finally { $zip.Dispose() }
