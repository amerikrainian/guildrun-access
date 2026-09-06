using System;
using Il2CppInterop.Runtime;

namespace GuildrunAccess.Module.Interop
{
    /// <summary>
    /// Reads the game's <c>Nullable&lt;struct&gt;</c> values correctly. IL2CPP boxes a nullable as a
    /// boxed value of the underlying struct (or null), but the interop proxy's <c>Value</c> treats the
    /// box as a nullable layout and reads the struct a few bytes off (a hero id comes back shifted), and
    /// its <c>HasValue</c> throws on null. The proxy's pointer IS the boxed struct, so unbox that.
    /// </summary>
    internal static class Nullables
    {
        /// <summary>The value behind an interop nullable, or false when it is null or unreadable.</summary>
        public static bool TryGet<T>(Il2CppSystem.Nullable<T> nullable, out T value) where T : new()
        {
            value = default;
            try
            {
                if (nullable == null) return false;
                var ptr = nullable.Pointer;
                if (ptr == IntPtr.Zero) return false;
                value = IL2CPP.PointerToValueGeneric<T>(ptr, false, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Run a nullable-returning getter (which itself can throw on null) and unbox the result.</summary>
        public static bool TryGet<T>(Func<Il2CppSystem.Nullable<T>> getter, out T value) where T : new()
        {
            value = default;
            try
            {
                return TryGet(getter(), out value);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
