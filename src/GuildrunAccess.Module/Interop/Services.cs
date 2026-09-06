using System;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VContainer.Unity;

namespace GuildrunAccess.Module.Interop
{
    /// <summary>
    /// Resolves the game's plain (non-MonoBehaviour) services from its VContainer scopes: every scene
    /// scope (application, run, battle) is a <see cref="LifetimeScope"/> in the scene, and a service
    /// registered in one resolves from its container. Scopes are re-scanned (throttled) when the set
    /// changes, since the run and battle scopes come and go.
    /// </summary>
    internal static class Services
    {
        private static LifetimeScope[] _scopes = new LifetimeScope[0];
        private const int SearchEvery = 60;
        private static int _lastSearchFrame = -SearchEvery;

        /// <summary>The service of type <typeparamref name="T"/> from whichever live scope registers it, or null.</summary>
        public static T Resolve<T>() where T : Il2CppObjectBase
        {
            foreach (var scope in Scopes())
            {
                if (scope == null) continue;
                try
                {
                    var container = scope.Container;
                    if (container == null) continue;
                    var resolved = container.Resolve(Il2CppType.Of<T>());
                    var typed = resolved != null ? resolved.TryCast<T>() : null;
                    if (typed != null) return typed;
                }
                catch (Exception)
                {
                    // Not registered in this scope (the container throws); try the next.
                }
            }
            return null;
        }

        private static LifetimeScope[] Scopes()
        {
            bool stale = false;
            foreach (var s in _scopes)
                if (s == null) { stale = true; break; }
            if (!stale && _scopes.Length > 0 && Time.frameCount - _lastSearchFrame < SearchEvery) return _scopes;
            if (Time.frameCount - _lastSearchFrame < SearchEvery) return _scopes;
            _lastSearchFrame = Time.frameCount;
            try
            {
                var found = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<LifetimeScope>());
                var list = new System.Collections.Generic.List<LifetimeScope>();
                foreach (var o in found)
                {
                    var scope = o != null ? o.TryCast<LifetimeScope>() : null;
                    if (scope != null) list.Add(scope);
                }
                _scopes = list.ToArray();
            }
            catch (Exception e)
            {
                CoreLog.Warning("Services: scope scan failed: " + e.Message);
            }
            return _scopes;
        }
    }
}
