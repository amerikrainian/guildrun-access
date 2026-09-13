// Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
// https://github.com/rashadnaqeeb/NonVisualCalculus

use std::path::{Path, PathBuf};

// The full release list, newest first: one call serves both the latest-version
// lookup and the per-version release notes shown after an update.
pub const GITHUB_RELEASES_URL: &str =
    "https://api.github.com/repos/amerikrainian/guildrun-access/releases?per_page=100";
pub const MOD_ZIP_PREFIX: &str = "GuildrunAccess-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
pub const GAME_EXES: &[&str] = &["Guildrun.exe"];
// Steam's demo install folder; the plain name covers the full release and a hand-moved copy.
pub const GAME_FOLDERS: &[&str] = &["Guildrun Demo", "Guildrun"];
// The game is IL2CPP: its code lives in GameAssembly.dll with the metadata beside the data
// folder, in every install, so together with the exe it identifies the game dir.
pub const GAME_ASSEMBLY_MARKER: &str = "Guildrun_Data/il2cpp_data/Metadata/global-metadata.dat";
pub const PLUGIN_REL: &str = "BepInEx/plugins/GuildrunAccess/GuildrunAccess.dll";
pub const MANIFEST_REL: &str = "BepInEx/config/GuildrunAccess/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/GuildrunAccess/backups";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}

pub fn required_loader_files() -> &'static [&'static str] {
    &[
        "winhttp.dll",
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.Core.dll",
        PLUGIN_REL,
    ]
}
