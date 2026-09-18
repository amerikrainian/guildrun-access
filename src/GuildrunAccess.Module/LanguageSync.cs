using System;
using System.IO;
using GuildrunAccess.Core;
using GuildrunAccess.Core.Strings;
using GuildrunAccess.Module.Interop;
using Il2CppInterop.Runtime;
using UnityEngine.Localization.Settings;
// Not "Locale": the Il2Cpp proxies of mscorlib and Mono.Security each carry a global Locale of their own.
using GameLocale = UnityEngine.Localization.Locale;

namespace GuildrunAccess.Module
{
    /// <summary>
    /// The mod's authored strings follow the GAME's language (Unity Localization's selected locale,
    /// which the settings screen's language dropdown sets and Unity's own PlayerPrefs selector restores
    /// at boot), not the system's: lang/&lt;locale code&gt;.txt beside the plugin, English (the defaults)
    /// when there is no file for it. Nothing is polled. A switch arrives through
    /// <c>LocalizationSettings.SelectedLocaleChanged</c>, which Unity raises inside the setter, so the
    /// table has changed before the control that committed the switch is read back. The first read
    /// waits for the first game scope (<see cref="GameScopes.Registered"/>: the application scope's
    /// build at boot, the seed on a hot reload): at plugin load the engine has no localization
    /// settings to ask yet, and the getter would force their load then and there.
    /// </summary>
    internal static class LanguageSync
    {
        private static string _langDir;
        private static string _applied;
        private static Action _onFirstLanguage;
        private static Il2CppSystem.Action<GameLocale> _hook;

        /// <summary>Whether the game's language has been read: the launch line and the update line
        /// wait for it, so they are spoken in the language the game came up in.</summary>
        public static bool Resolved => _applied != null;

        /// <summary>The locale code whose strings are in effect ("de"), or null before the first read.</summary>
        public static string Applied => _applied;

        /// <summary>Begin following the game's language. <paramref name="onFirstLanguage"/> runs once,
        /// right after the first language is in place. Call before <see cref="GameScopes.Seed"/>.</summary>
        public static void Start(string pluginDir, Action onFirstLanguage)
        {
            _langDir = Path.Combine(pluginDir, "lang");
            _onFirstLanguage = onFirstLanguage;
            GameScopes.Registered += OnScopeRegistered;
        }

        /// <summary>Drop the locale hook (module teardown; a successor has its own).</summary>
        public static void Stop()
        {
            GameScopes.Registered -= OnScopeRegistered;
            if (_hook == null) return;
            try { LocalizationSettings.remove_SelectedLocaleChanged(_hook); }
            catch (Exception e) { CoreLog.Warning("LanguageSync: unhooking the locale event failed: " + e.Message); }
            _hook = null;
        }

        // A game scope exists, so the engine is up: hook the locale event once, and read the language
        // until one read lands (a later scope retries a read that came back empty).
        private static void OnScopeRegistered()
        {
            if (_hook == null) Hook();
            if (!Resolved) Apply(CurrentCode());
        }

        private static void Hook()
        {
            try
            {
                _hook = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameLocale>>(new Action<GameLocale>(OnLocaleChanged));
                LocalizationSettings.add_SelectedLocaleChanged(_hook);
            }
            catch (Exception e)
            {
                CoreLog.Error("LanguageSync: hooking the locale event failed, a language switch will not be followed: " + e);
                _hook = null;
            }
        }

        // Raised by Unity inside the locale setter. Never let a fault of ours escape into it.
        private static void OnLocaleChanged(GameLocale locale)
        {
            try { Apply(CodeOf(locale)); }
            catch (Exception e) { CoreLog.Error("LanguageSync: applying a language switch failed: " + e); }
        }

        private static string CurrentCode()
        {
            try { return CodeOf(LocalizationSettings.SelectedLocale); }
            catch (Exception e)
            {
                CoreLog.Warning("LanguageSync: the game's language could not be read: " + e.Message);
                return null;
            }
        }

        private static string CodeOf(GameLocale locale)
        {
            if (locale == null) return null;
            string code = locale.Identifier.Code;
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }

        private static void Apply(string code)
        {
            if (code == null || code == _applied) return;
            bool first = _applied == null;
            _applied = code;
            Load(code);
            if (!first || _onFirstLanguage == null) return;
            try { _onFirstLanguage(); }
            catch (Exception e) { CoreLog.Error("LanguageSync: the first-language callback failed: " + e); }
        }

        private static void Load(string code)
        {
            try
            {
                foreach (var name in LanguageFiles.Candidates(code))
                {
                    string path = Path.Combine(_langDir, name);
                    if (!File.Exists(path)) continue;
                    var report = Strings.LoadTranslation(File.ReadAllLines(path));
                    foreach (var problem in report.Problems()) CoreLog.Warning("lang " + name + ": " + problem);
                    CoreLog.Info("LanguageSync: " + code + ", " + report.Applied + " strings from " + name);
                    return;
                }
                Strings.LoadTranslation(null);
                CoreLog.Info("LanguageSync: " + code + ", no lang file for it, English");
            }
            catch (Exception e)
            {
                Strings.LoadTranslation(null);
                CoreLog.Error("LanguageSync: loading the strings of " + code + " failed, English: " + e);
            }
        }
    }
}
