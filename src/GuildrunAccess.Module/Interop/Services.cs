using System;
using GuildrunAccess.Core;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using VContainer.Unity;

namespace GuildrunAccess.Module.Interop
{
    /// <summary>
    /// Resolves the game's plain (non-MonoBehaviour) services from its VContainer scopes: every area
    /// scope (application, run, battle) is a <see cref="LifetimeScope"/>, and a service registered in
    /// one resolves from its container. The live scopes come from <see cref="GameScopes"/>.
    /// </summary>
    internal static class Services
    {
        /// <summary>The service of type <typeparamref name="T"/> from whichever live scope registers it, or null.</summary>
        public static T Resolve<T>() where T : Il2CppObjectBase
        {
            foreach (var scope in GameScopes.Scopes)
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

    }
}
