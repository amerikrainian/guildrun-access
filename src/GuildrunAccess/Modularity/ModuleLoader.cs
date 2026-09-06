using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using BepInEx.Logging;
using GuildrunAccess.Contracts;

namespace GuildrunAccess.Modularity
{
    /// <summary>
    /// Loads, unloads, and reloads the feature side without restarting the game. Both reloadable DLLs
    /// (Core, then Module) are read into memory and loaded from their bytes (never from their paths) so
    /// the on-disk files stay unlocked and dotnet build can overwrite them while the game runs. Each load
    /// gets a fresh collectible <see cref="AssemblyLoadContext"/> holding exactly those two assemblies;
    /// every other dependency (Contracts, the Il2Cpp proxies, BepInEx, Harmony) resolves back to its
    /// already-loaded default-context copy, which keeps the Contracts type identity stable across the
    /// boundary. The host never references Core at compile time, so Core is never loaded twice.
    /// </summary>
    internal sealed class ModuleLoader
    {
        private const string CoreAssemblyName = "GuildrunAccess.Core";

        private readonly string _corePath;
        private readonly string _modulePath;
        private readonly IModHost _host;
        private readonly ManualLogSource _log;

        private ModuleAlc _alc;

        public ModuleLoader(string corePath, string modulePath, IModHost host, ManualLogSource log)
        {
            _corePath = corePath;
            _modulePath = modulePath;
            _host = host;
            _log = log;
        }

        /// <summary>The live module instance, or null if no load has succeeded. Read fresh each frame.</summary>
        public IModModule Module { get; private set; }

        /// <summary>How many loads have succeeded this process (1 = the boot load; each successful
        /// reload increments).</summary>
        public int Generation { get; private set; }

        public string CorePath => _corePath;
        public string ModulePath => _modulePath;

        /// <summary>Load Core + Module from their current bytes. Returns false (and logs) on any failure.</summary>
        public bool Load()
        {
            ModuleAlc candidateAlc = null;
            try
            {
                if (!File.Exists(_corePath))
                {
                    _log.LogError("ModuleLoader: Core DLL not found at " + _corePath);
                    return false;
                }
                if (!File.Exists(_modulePath))
                {
                    _log.LogError("ModuleLoader: module DLL not found at " + _modulePath);
                    return false;
                }

                byte[] coreBytes = File.ReadAllBytes(_corePath);
                byte[] moduleBytes = File.ReadAllBytes(_modulePath);
                candidateAlc = new ModuleAlc(coreBytes);
                Assembly asm;
                using (var ms = new MemoryStream(moduleBytes))
                    asm = candidateAlc.LoadFromStream(ms);

                Type type = asm.GetTypes().FirstOrDefault(
                    t => typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                if (type == null)
                {
                    _log.LogError("ModuleLoader: no IModModule implementor in " + asm.GetName().Name);
                    candidateAlc.Unload();
                    return false;
                }

                var module = (IModModule)Activator.CreateInstance(type);
                module.Load(_host);

                // Swap in only once the new module is fully live, so a failed reload (locked / corrupt /
                // half-written DLL) leaves the running module untouched rather than tearing it down.
                DisposeCurrent();
                _alc = candidateAlc;
                Module = module;
                Generation++;
                _log.LogInfo("ModuleLoader: loaded " + type.FullName + " (generation " + Generation + ")");
                return true;
            }
            catch (Exception ex)
            {
                _log.LogError("ModuleLoader.Load failed: " + ex);
                if (candidateAlc != null)
                {
                    try { candidateAlc.Unload(); }
                    catch (Exception e) { _log.LogWarning("ModuleLoader: candidate ALC unload threw: " + e); }
                }
                return false;
            }
        }

        /// <summary>Tear down the current module and drop its collectible context (for shutdown).</summary>
        public void Unload() => DisposeCurrent();

        /// <summary>Rebuild from the current bytes, swapping the new module in only if it loads cleanly.</summary>
        public bool Reload() => Load();

        // Dispose the live module and unload its context (old types leak until GC).
        private void DisposeCurrent()
        {
            if (Module != null)
            {
                try { Module.Dispose(); }
                catch (Exception ex) { _log.LogError("ModuleLoader: module Dispose threw: " + ex); }
                Module = null;
            }

            if (_alc != null)
            {
                try { _alc.Unload(); }
                catch (Exception ex) { _log.LogWarning("ModuleLoader: ALC unload threw: " + ex); }
                _alc = null;
            }
        }

        // Collectible context for the reloadable pair. Core resolves to a byte-loaded copy INSIDE this
        // context (so it reloads with the module); every other name returns null, deferring to the
        // default context, so the module shares the host's single copy of Contracts / the Il2Cpp proxies
        // / BepInEx rather than loading its own.
        private sealed class ModuleAlc : AssemblyLoadContext
        {
            private readonly byte[] _coreBytes;
            private Assembly _core;

            public ModuleAlc(byte[] coreBytes) : base(isCollectible: true) { _coreBytes = coreBytes; }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (assemblyName.Name == CoreAssemblyName)
                {
                    if (_core == null)
                        using (var ms = new MemoryStream(_coreBytes))
                            _core = LoadFromStream(ms);
                    return _core;
                }
                return null;
            }
        }
    }
}
