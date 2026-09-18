using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GuildrunAccess.Contracts;
using GuildrunAccess.Dev;
using GuildrunAccess.Host;
using GuildrunAccess.Modularity;
using GuildrunAccess.Speech;
using Il2CppInterop.Runtime.Injection;

namespace GuildrunAccess
{
    /// <summary>
    /// The permanent host: only what can never reload. Stands up Prism, the speech pipeline, the settings
    /// store, the dev server, the one injected pump, and the module loader, then hands per-frame work to
    /// the reloadable side (Core + Module). No feature logic lives here.
    /// </summary>
    [BepInPlugin(Guid, Name, BuildVersion.Value)]
    public sealed class Plugin : BasePlugin
    {
        public const string Guid = "com.amerikranian.guildrunaccess";
        public const string Name = "Guildrun Access";
        public const string Version = BuildVersion.Value;

        internal static ManualLogSource Logger;

        private PrismBackend _prism;
        private ModuleLoader _loader;
#if DEBUG
        private DevServer _devServer;
#endif

        public override void Load()
        {
            Logger = Log;
            Log.LogInfo($"{Name} {Version} loading");

            // Headless dev runs set GRA_NO_SPEECH=1 so an unattended session does not depend on a running
            // screen reader. Prism init is skipped; spoken text is still captured for /speech, since the
            // dev tap is upstream of the backend.
            bool muteSpeech = System.Environment.GetEnvironmentVariable("GRA_NO_SPEECH") == "1";
            _prism = new PrismBackend(Log);
            if (muteSpeech)
                Log.LogInfo("Speech muted (GRA_NO_SPEECH=1); spoken text still captured for /speech");
            else
                _prism.Initialize();

            var speech = new SpeechPipeline(_prism) { Muted = muteSpeech };

            // Settings persist through our BepInEx config file and are owned here (permanent) so they
            // survive a module hot-reload; the module reads them through IModHost.Settings.
            var settings = new ModSettings(new BepInExSettingsStore(Config));

            string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            var host = new ModHost(Log, speech, settings, pluginDir);

            _loader = new ModuleLoader(
                Path.Combine(pluginDir, "GuildrunAccess.Core.dll"),
                Path.Combine(pluginDir, "GuildrunAccess.Module.dll"),
                host, Log);

#if DEBUG
            // Dev-only: the loopback server exposes a C# eval REPL; never in a player build. Started first
            // so its speech tap catches the load line.
            _devServer = new DevServer(_loader, Log);
            _devServer.Start();
#endif

            // The "loaded" line is the module's: it has the strings table, and says the line once the
            // game's language is known (IModHost.LaunchAnnounced). The module carries every feature;
            // if it fails to load the player would otherwise get
            // silence with no signal, so say so out loud (the cause is in the log).
            if (!_loader.Load())
                speech.Speak(Name + " features failed to load", interrupt: false);

            ClassInjector.RegisterTypeInIl2Cpp<HostPump>();
            AddComponent<HostPump>();
            HostPump.Loader = _loader;
#if DEBUG
            HostPump.DevServer = _devServer;
#endif

            Log.LogInfo($"{Name} loaded");
        }
    }
}
