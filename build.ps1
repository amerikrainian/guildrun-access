# build.ps1 - Build Guildrun Access and deploy it into the game. Deploy itself is the host project's
# Debug post-build target (plugin + Core + Module into BepInEx\plugins\GuildrunAccess, prism.dll next
# to Guildrun.exe); this script checks BepInEx is set up and runs the build. Close the game first for a
# HOST change (its DLL is locked while the game runs); Core/Module changes deploy fine with the game
# running and take effect on POST /reload or F6.

param(
    [switch]$Help,
    [switch]$ModuleOnly
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-ModuleOnly] [-Help]"
    Write-Host "  Builds the solution and deploys the mod into the game folder."
    Write-Host "  -ModuleOnly builds just the reloadable Core + Module (the hot loop)."
    Write-Host "  Run setup-bepinex.ps1 once first, then launch the game once through Steam."
    exit 0
}

$ErrorActionPreference = "Stop"

$Game = $env:GUILDRUN_DIR
if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo" }
if (-not (Test-Path "$Game\Guildrun.exe")) {
    Write-Host "ERROR: Guildrun Demo not found at: $Game (set GUILDRUN_DIR)" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\core\BepInEx.Core.dll")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx. Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\interop")) {
    Write-Host "ERROR: $Game\BepInEx\interop does not exist. Launch the game once through Steam" -ForegroundColor Red
    Write-Host "(steam.exe -applaunch 4425970) and wait for the main menu, then re-run this." -ForegroundColor Red
    exit 1
}

if ($ModuleOnly) {
    dotnet build "$PSScriptRoot\src\GuildrunAccess.Module\GuildrunAccess.Module.csproj" -c Debug
} else {
    dotnet build "$PSScriptRoot\GuildrunAccess.slnx" -c Debug
}
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Done. Launch Guildrun through Steam and listen for the startup line, or POST /reload if it is running." -ForegroundColor Cyan
