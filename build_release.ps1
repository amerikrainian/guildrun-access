# Build the distributable mod zip: the vendored BepInEx 6 game-folder layout (BepInEx\, dotnet\,
# winhttp.dll, doorstop_config.ini) plus the Release plugin output under
# BepInEx\plugins\GuildrunAccess (the host, Contracts, Core and Module dlls, lang files), prism.dll
# next to Guildrun.exe, a BepInEx.cfg that keeps the loader's console window closed, and the
# rendered mdbook manual under GuildrunAccessDocs when docs_src exists. The zip root IS the game
# folder, so the installer (and a manual user) extracts it straight into the game dir.
#
# The Roslyn scripting assemblies in the host's output are the Debug-only dev server's and are not
# shipped: the Release host never instantiates it.
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$propsPath = Join-Path $scriptDir "Directory.Build.props"
$releaseDir = Join-Path $scriptDir "releases"
$stageDir = Join-Path $scriptDir "obj\release-stage"

[xml]$props = Get-Content $propsPath
$versionNode = $props.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Could not read Version from $propsPath"
}
$version = $versionNode.InnerText.Trim()

# The vendored loader is a zip whose root is the game-folder layout; its changelog.txt is
# BepInEx's own release notes and is not shipped.
$bepinexZip = Get-ChildItem (Join-Path $scriptDir "third_party\bepinex\BepInEx-Unity.IL2CPP-win-x64-*.zip") |
    Select-Object -First 1
$prismDll = Join-Path $scriptDir "third_party\prism\prism.dll"
$hostOutDir = Join-Path $scriptDir "src\GuildrunAccess\bin\Release\net6.0"
$coreOutDir = Join-Path $scriptDir "src\GuildrunAccess.Core\bin\Release\netstandard2.0"
$moduleOutDir = Join-Path $scriptDir "src\GuildrunAccess.Module\bin\Release\net6.0"
$docsDir = Join-Path $scriptDir "docs_src"
$langDir = Join-Path $scriptDir "lang"
$zipPath = Join-Path $releaseDir "GuildrunAccess-v$version.zip"

if ($null -eq $bepinexZip) {
    throw "Vendored BepInEx zip not found under third_party\bepinex"
}
if (-not (Test-Path $prismDll)) {
    throw "Required file not found: $prismDll"
}

Push-Location $scriptDir
try {
    # The dlls are picked from the output dirs by name, so a stale DLL there (a renamed or removed
    # assembly dotnet build no longer owns) would ship. Start them empty.
    foreach ($dir in @($hostOutDir, $coreOutDir, $moduleOutDir)) {
        if (Test-Path $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force
        }
    }

    dotnet build GuildrunAccess.slnx -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    $hasDocs = Test-Path (Join-Path $docsDir "book.toml")
    if ($hasDocs) {
        mdbook build $docsDir
        if ($LASTEXITCODE -ne 0) {
            throw "Docs build failed with exit code $LASTEXITCODE"
        }
    }

    $modDlls = @(
        (Join-Path $hostOutDir "GuildrunAccess.dll"),
        (Join-Path $hostOutDir "GuildrunAccess.Contracts.dll"),
        (Join-Path $coreOutDir "GuildrunAccess.Core.dll"),
        (Join-Path $moduleOutDir "GuildrunAccess.Module.dll")
    )
    foreach ($required in $modDlls) {
        if (-not (Test-Path $required)) {
            throw "Release build output not found: $required"
        }
    }

    if (Test-Path $stageDir) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force $stageDir | Out-Null
    New-Item -ItemType Directory -Force $releaseDir | Out-Null

    Expand-Archive -Path $bepinexZip.FullName -DestinationPath $stageDir -Force
    $loaderNotes = Join-Path $stageDir "changelog.txt"
    if (Test-Path $loaderNotes) {
        Remove-Item -LiteralPath $loaderNotes -Force
    }
    foreach ($required in @("winhttp.dll", "doorstop_config.ini", "BepInEx\core\BepInEx.Core.dll")) {
        if (-not (Test-Path (Join-Path $stageDir $required))) {
            throw "The vendored BepInEx zip lacks $required"
        }
    }

    # No BepInEx console window: the mod speaks, and a second window only steals focus from the
    # screen reader. BepInEx keeps the values it finds in BepInEx.cfg and fills in the rest on the
    # first launch, so seeding this one key is enough (setup-bepinex.ps1 does the same for a dev box).
    $configDir = Join-Path $stageDir "BepInEx\config"
    New-Item -ItemType Directory -Force $configDir | Out-Null
    [IO.File]::WriteAllText((Join-Path $configDir "BepInEx.cfg"), "[Logging.Console]`r`nEnabled = false`r`n")

    # The same file set the Debug post-build targets deploy, minus the dev server's Roslyn deps.
    $pluginDir = Join-Path $stageDir "BepInEx\plugins\GuildrunAccess"
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    foreach ($dll in $modDlls) {
        Copy-Item -LiteralPath $dll -Destination $pluginDir
    }
    Copy-Item -LiteralPath $prismDll -Destination $stageDir

    if (Test-Path $langDir) {
        $langFiles = Get-ChildItem (Join-Path $langDir "*.txt")
        if ($langFiles.Count -gt 0) {
            $stageLangDir = Join-Path $pluginDir "lang"
            New-Item -ItemType Directory -Force $stageLangDir | Out-Null
            Copy-Item -Path $langFiles.FullName -Destination $stageLangDir
        }
    }

    # The sound cues' files (assets\audio\<group>\<cue>.wav), under the plugin's assets folder.
    $audioDir = Join-Path $scriptDir "assets\audio"
    if (Test-Path $audioDir) {
        $stageAssetsDir = Join-Path $pluginDir "assets"
        New-Item -ItemType Directory -Force $stageAssetsDir | Out-Null
        Copy-Item -Path $audioDir -Destination $stageAssetsDir -Recurse
    }

    if ($hasDocs) {
        # Packaging pattern adapted from SayTheSpire2: https://github.com/bradjrenshaw/say-the-spire2
        Copy-Item -Path (Join-Path $docsDir "book") -Destination (Join-Path $stageDir "GuildrunAccessDocs") -Recurse
    }

    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force

    Remove-Item -LiteralPath $stageDir -Recurse -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
