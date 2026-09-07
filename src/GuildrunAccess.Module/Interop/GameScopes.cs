using System;
using System.Collections.Generic;
using gg.leyline.core.Bootstrap;
using gg.leyline.core.Mvcs;
using GuildrunAccess.Core;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VContainer.Unity;

namespace GuildrunAccess.Module.Interop
{
    /// <summary>
    /// The game's live VContainer scopes and, through them, every controller and view the mod reads,
    /// with no scene scans. Each area of the game (application, main menu, run, battle, event, shop,
    /// campfire) is a <see cref="LifetimeScope"/>; the game's <see cref="BootstrappedScope"/> keeps the
    /// list of the <see cref="MonoBehaviourController"/>s of its area and injects them when the scope
    /// builds. Harmony postfixes on that build path register the scope the moment it exists (the module
    /// loads before the first scope, so nothing is missed at boot); a scope leaving is dropped by its
    /// OnDestroy postfix, or when its proxy reads as destroyed.
    ///
    /// <see cref="Controller{T}"/> walks the scopes' controller lists (a few dozen entries, sub-microsecond
    /// each) and caches per type until the scope set changes; among several instances the active one
    /// wins, the earliest scope's on a tie. <see cref="Component{T}"/> serves views that are not
    /// controllers (dialog panels, tutorial text): found once per scope arrival under the scope's own
    /// hierarchy, or its scene's roots for a scope that owns no children (the battle scene).
    ///
    /// The one scene scan left is <see cref="Seed"/> on a hot reload: the scopes already exist and no
    /// build will fire for them. At a normal boot it finds nothing.
    /// </summary>
    internal static class GameScopes
    {
        private sealed class Entry
        {
            public LifetimeScope Scope;
            public BootstrappedScope Bootstrapped; // null for a scope without a controller list
            public string Name;
            /// <summary>Non-controller views found under this scope, per requested class pointer.</summary>
            public readonly Dictionary<IntPtr, object> Found = new Dictionary<IntPtr, object>();
        }

        private static readonly List<Entry> _entries = new List<Entry>(); // arrival order
        private static int _version;      // bumps on every arrival/departure; per-type caches key on it
        private static int _prunedFrame = -1;

        // Per requested type: the candidates across all scopes, valid for one version.
        private static class Typed<T> where T : Component
        {
            public static int ControllersVersion = -1;
            public static List<T> Controllers;
            public static int ComponentsVersion = -1;
            public static List<T> Components;
        }

        /// <summary>The live scopes, arrival order (for the containers their services resolve from).</summary>
        public static IReadOnlyList<LifetimeScope> Scopes
        {
            get
            {
                Prune();
                if (_scopesVersion != _version)
                {
                    var list = new List<LifetimeScope>(_entries.Count);
                    foreach (var e in _entries) list.Add(e.Scope);
                    _scopes = list;
                    _scopesVersion = _version;
                }
                return _scopes;
            }
        }
        private static List<LifetimeScope> _scopes = new List<LifetimeScope>();
        private static int _scopesVersion = -1;

        /// <summary>The live controller of type <typeparamref name="T"/> (or a subclass): the active
        /// instance when there are several, else the first; null when no scope has one.</summary>
        public static T Controller<T>() where T : MonoBehaviour
        {
            Prune();
            if (Typed<T>.ControllersVersion != _version)
            {
                Typed<T>.Controllers = CollectControllers<T>();
                Typed<T>.ControllersVersion = _version;
            }
            return Pick(Typed<T>.Controllers);
        }

        /// <summary>A view of type <typeparamref name="T"/> that is not a controller: the active instance
        /// when there are several, else the first; null when none exists.</summary>
        public static T Component<T>() where T : Component => Pick(Components<T>());

        /// <summary>Every instance of <typeparamref name="T"/> under the live scopes (destroyed ones
        /// included until the next scope change: check for null).</summary>
        public static List<T> Components<T>() where T : Component
        {
            Prune();
            if (Typed<T>.ComponentsVersion != _version)
            {
                Typed<T>.Components = CollectComponents<T>();
                Typed<T>.ComponentsVersion = _version;
            }
            return Typed<T>.Components;
        }

        /// <summary>One line per live scope, for the log.</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in _entries)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(e.Name);
                if (e.Bootstrapped != null) sb.Append(" (").Append(ControllerCount(e)).Append(" controllers)");
            }
            return sb.Length > 0 ? sb.ToString() : "(none)";
        }

        // ---- lifecycle ----

        /// <summary>Register the scopes that already exist (a hot reload: their builds fired before this
        /// load). The one scene scan; at boot there is nothing to find.</summary>
        public static void Seed()
        {
            try
            {
                var found = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<LifetimeScope>(), true);
                foreach (var o in found)
                {
                    var scope = o != null ? o.TryCast<LifetimeScope>() : null;
                    if (scope != null) Register(scope, "seed");
                }
                CoreLog.Info("GameScopes: seeded " + _entries.Count + " scope(s): " + Describe());
            }
            catch (Exception e)
            {
                CoreLog.Error("GameScopes: seed failed: " + e);
            }
        }

        /// <summary>Forget every scope (module teardown; the successor seeds itself).</summary>
        public static void Shutdown()
        {
            _entries.Clear();
            _version++;
        }

        private static void Register(LifetimeScope scope, string source)
        {
            try
            {
                if (scope == null) return;
                var ptr = scope.Pointer;
                foreach (var e in _entries)
                    if (e.Scope.Pointer == ptr) return; // both build hooks fire for one scope
                var entry = new Entry
                {
                    Scope = scope,
                    Bootstrapped = scope.TryCast<BootstrappedScope>(),
                    Name = scope.name,
                };
                _entries.Add(entry);
                _version++;
                CoreLog.Info("GameScopes: + " + entry.Name + " [" + source + "]"
                    + (entry.Bootstrapped != null ? ", " + ControllerCount(entry) + " controllers" : ""));
            }
            catch (Exception e)
            {
                CoreLog.Error("GameScopes: register failed: " + e);
            }
        }

        private static void Unregister(LifetimeScope scope)
        {
            try
            {
                if (scope == null) return;
                var ptr = scope.Pointer;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Scope.Pointer != ptr) continue;
                    CoreLog.Info("GameScopes: - " + _entries[i].Name);
                    _entries.RemoveAt(i);
                    _version++;
                    return;
                }
            }
            catch (Exception e)
            {
                CoreLog.Error("GameScopes: unregister failed: " + e);
            }
        }

        // A scope destroyed without its OnDestroy postfix (an override that skips the base) reads as
        // null through Unity's lifetime check: drop it. Once per frame.
        private static void Prune()
        {
            if (Time.frameCount == _prunedFrame) return;
            _prunedFrame = Time.frameCount;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Scope != null) continue;
                CoreLog.Info("GameScopes: - " + _entries[i].Name + " (destroyed)");
                _entries.RemoveAt(i);
                _version++;
            }
        }

        // ---- lookups ----

        private static int ControllerCount(Entry e)
        {
            try { var list = e.Bootstrapped._monoBehaviourControllers; return list != null ? list.Count : 0; }
            catch (Exception) { return -1; }
        }

        private static List<T> CollectControllers<T>() where T : MonoBehaviour
        {
            var result = new List<T>();
            foreach (var e in _entries)
            {
                if (e.Bootstrapped == null) continue;
                try
                {
                    var list = e.Bootstrapped._monoBehaviourControllers;
                    if (list == null) continue;
                    int n = list.Count;
                    for (int i = 0; i < n; i++)
                    {
                        var c = list[i];
                        var typed = c != null ? c.TryCast<T>() : null;
                        if (typed != null) result.Add(typed);
                    }
                }
                catch (Exception ex)
                {
                    CoreLog.Warning("GameScopes: controller list of " + e.Name + " unreadable: " + ex.Message);
                }
            }
            return result;
        }

        private static List<T> CollectComponents<T>() where T : Component
        {
            var result = new List<T>();
            var key = Il2CppClassPointerStore<T>.NativeClassPtr;
            foreach (var e in _entries)
            {
                if (!e.Found.TryGetValue(key, out var boxed))
                {
                    boxed = SearchScope<T>(e);
                    e.Found[key] = boxed;
                }
                result.AddRange((List<T>)boxed);
            }
            return result;
        }

        // Under the scope's own hierarchy, and for a scope that owns no children (the battle scene's
        // scope object) under its scene's roots. Once per scope and type.
        private static List<T> SearchScope<T>(Entry e) where T : Component
        {
            var result = new List<T>();
            try
            {
                var type = Il2CppType.Of<T>();
                var root = e.Scope.gameObject;
                AddComponents(result, root.GetComponentsInChildren(type, true));
                if (root.transform.childCount == 0)
                {
                    var scene = root.scene;
                    if (scene.IsValid())
                        foreach (var sceneRoot in scene.GetRootGameObjects())
                            if (sceneRoot != null && sceneRoot.Pointer != root.Pointer)
                                AddComponents(result, sceneRoot.GetComponentsInChildren(type, true));
                }
            }
            catch (Exception ex)
            {
                CoreLog.Warning("GameScopes: search under " + e.Name + " failed: " + ex.Message);
            }
            return result;
        }

        private static void AddComponents<T>(List<T> into, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Component> found) where T : Component
        {
            if (found == null) return;
            foreach (var c in found)
            {
                var typed = c != null ? c.TryCast<T>() : null;
                if (typed != null) into.Add(typed);
            }
        }

        // The active instance, else the first live one. A destroyed candidate is dropped from the cache.
        private static T Pick<T>(List<T> candidates) where T : Component
        {
            if (candidates == null) return null;
            T first = null;
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (candidates[i] == null) candidates.RemoveAt(i);
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.gameObject.activeInHierarchy) return c;
                if (first == null) first = c;
            }
            return first;
        }

        // ---- the hooks: the game's own scope lifecycle ----

        /// <summary>The bootstrapped scope injects its controllers from its list: the list is complete here.</summary>
        [HarmonyPatch(typeof(BootstrappedScope), "InjectControllers")]
        private static class InjectControllersPatch
        {
            private static void Postfix(BootstrappedScope __instance) => Register(__instance, "inject");
        }

        /// <summary>Every VContainer scope, bootstrapped or not, once its container is built.</summary>
        [HarmonyPatch(typeof(LifetimeScope), nameof(LifetimeScope.Build))]
        private static class BuildPatch
        {
            private static void Postfix(LifetimeScope __instance) => Register(__instance, "build");
        }

        [HarmonyPatch(typeof(BootstrappedScope), "OnDestroy")]
        private static class BootstrappedDestroyPatch
        {
            private static void Postfix(BootstrappedScope __instance) => Unregister(__instance);
        }

        [HarmonyPatch(typeof(LifetimeScope), "OnDestroy")]
        private static class DestroyPatch
        {
            private static void Postfix(LifetimeScope __instance) => Unregister(__instance);
        }
    }
}
