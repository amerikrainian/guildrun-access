# setup-bepinex.ps1 - Install the vendored BepInEx (third_party/bepinex) into the Guildrun Demo
# folder. Idempotent: safe to re-run (e.g. after a game update wipes it). After this, launch the
# game once through Steam (the first launch generates the BepInEx\interop proxy assemblies the
# build compiles against; it takes a few minutes), then run build.ps1 to deploy the mod.

$ErrorActionPreference = "Stop"

# --- Locate the game install: GUILDRUN_DIR env var, else Steam library folders, else the default.
$Game = $env:GUILDRUN_DIR
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\Guildrun Demo"
        if (Test-Path "$candidate\Guildrun.exe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo" }
}
if (-not (Test-Path "$Game\Guildrun.exe")) {
    Write-Host "ERROR: Guildrun Demo not found at: $Game" -ForegroundColor Red
    Write-Host "Set the GUILDRUN_DIR environment variable to the game folder." -ForegroundColor Red
    exit 1
}

$Zip = Get-ChildItem "$PSScriptRoot\third_party\bepinex\BepInEx-Unity.IL2CPP-win-x64-*.zip" | Select-Object -First 1
if (-not $Zip) {
    Write-Host "ERROR: vendored BepInEx zip not found under third_party\bepinex" -ForegroundColor Red
    exit 1
}

Write-Host "Installing $($Zip.Name) into $Game ..." -ForegroundColor Cyan

# The zip root is the game-folder layout (winhttp.dll, doorstop_config.ini, BepInEx\, dotnet\),
# so it extracts straight into the game folder.
Expand-Archive -Path $Zip.FullName -DestinationPath $Game -Force

Write-Host ""
Write-Host "BepInEx installed. Now launch the game once through Steam (steam.exe -applaunch 4425970)" -ForegroundColor Cyan
Write-Host "and wait for the main menu; the first launch generates the interop assemblies the build" -ForegroundColor Cyan
Write-Host "needs and can take a few minutes. Then quit and run build.ps1 to deploy the mod." -ForegroundColor Cyan
