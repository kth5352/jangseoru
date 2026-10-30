# Installs a MINIMAL BepInEx (only what the plugin needs) plus the plugin into a VANILLA game folder so the
# plugin can be verified in the real game. Refuses to touch a folder that already has BepInEx/doorstop files.
# It does NOT install any other mod and does NOT enable the patched Addressables (dll_search_path_override is emptied).
# Everything added is recorded in a manifest outside the game folder; tools/uninstall-test-env.ps1 reverses it.
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\LegendOfMortal',
    [string]$ModZip = (Join-Path $PSScriptRoot '..\..\mod.zip'),
    [string]$PluginDist = (Join-Path $PSScriptRoot '..\dist\Jangseoru'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\..\backups\install-manifest.txt')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Get-Process -Name Mortal -ErrorAction SilentlyContinue) { throw 'The game is running. Close it first.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Mortal.exe'))) { throw "Not a game folder: $GameDir" }
foreach ($n in 'winhttp.dll', 'doorstop_config.ini', 'BepInEx') {
    if (Test-Path -LiteralPath (Join-Path $GameDir $n)) { throw "Refusing to install: '$n' already exists in the game folder (not vanilla)." }
}
foreach ($f in 'Jangseoru.Plugin.dll', 'Jangseoru.Core.dll') {
    if (-not (Test-Path -LiteralPath (Join-Path $PluginDist $f))) { throw "Plugin build missing: $f (run dotnet build + tools/package.ps1)" }
}

$added = New-Object System.Collections.Generic.List[string]
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ModZip))
try {
    foreach ($e in $zip.Entries) {
        $name = $e.FullName
        $isCore = $name.StartsWith('BepInEx/core/') -and $e.Name -ne ''
        if ($name -ne 'winhttp.dll' -and $name -ne 'doorstop_config.ini' -and -not $isCore) { continue }

        $dest = Join-Path $GameDir ($name -replace '/', '\')
        New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $false)
        $added.Add($name)
    }
}
finally { $zip.Dispose() }

# Do not load the mod pack's patched Unity.Addressables.dll: only this plugin is under test.
$ini = Join-Path $GameDir 'doorstop_config.ini'
$text = Get-Content -LiteralPath $ini -Raw
$text = [regex]::Replace($text, '(?m)^dll_search_path_override\s*=.*$', 'dll_search_path_override =')
[System.IO.File]::WriteAllText($ini, $text)

$pluginDir = Join-Path $GameDir 'BepInEx\plugins\Jangseoru'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
foreach ($f in 'Jangseoru.Plugin.dll', 'Jangseoru.Core.dll') {
    Copy-Item -LiteralPath (Join-Path $PluginDist $f) -Destination $pluginDir
    $added.Add("BepInEx/plugins/Jangseoru/$f")
}

New-Item -ItemType Directory -Force -Path (Split-Path $ManifestPath) | Out-Null
$added | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
Write-Host "installed $($added.Count) files into $GameDir; manifest: $ManifestPath"
