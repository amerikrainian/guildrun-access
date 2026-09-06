using GuildrunAccess.Core;
using UnityEngine.EventSystems;

namespace GuildrunAccess.Module.UI
{
    /// <summary>
    /// When active, our navigation owns the keyboard: the uGUI EventSystem stops sending navigation
    /// events (so arrows and Enter cannot also move or submit the game's own selection) and its selection
    /// is cleared. Fully reversible, and re-applied per frame because Unity rebuilds the EventSystem on
    /// scene changes. Our own keys are captured via InputManager's poll, independent of this.
    /// </summary>
    public static class FocusMode
    {
        private static bool _active;
        private static EventSystem _applied; // the EventSystem instance the suppression was applied to

        public static bool Active => _active;

        public static void Toggle() => Set(!_active);

        public static void Set(bool on)
        {
            if (on == _active) return;
            _active = on;
            if (on) Apply(EventSystem.current);
            else Release();
        }

        /// <summary>Per-frame: re-apply the suppression when the game constructs a fresh EventSystem, and
        /// reassert the flag on the current one. The reassert matters on a hot reload: the host loads the
        /// new generation (which suppresses) BEFORE disposing the old one (whose Release restores), so
        /// without it a reload would hand navigation back to the game.</summary>
        public static void Tick()
        {
            if (!_active) return;
            var es = EventSystem.current;
            if (es == null) return;
            if (!ReferenceEquals(es, _applied)) { Apply(es); return; }
            if (es.sendNavigationEvents) es.sendNavigationEvents = false;
        }

        private static void Apply(EventSystem es)
        {
            _applied = es;
            if (es == null)
            {
                CoreLog.Info("FocusMode: no EventSystem yet; will engage when one appears.");
                return;
            }
            es.sendNavigationEvents = false;
            es.SetSelectedGameObject(null);
        }

        private static void Release()
        {
            var es = _applied ?? EventSystem.current;
            _applied = null;
            if (es != null) es.sendNavigationEvents = true;
        }
    }
}
