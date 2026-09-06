using System.Text;
using GuildrunAccess.Core.UI;
using UnityEngine;

namespace GuildrunAccess.Module.Input
{
    /// <summary>
    /// The engine-backed <see cref="INavInput"/>: frame/time from UnityEngine.Time and keys from the
    /// legacy Input class. Typed text is polled per key (A..Z and space) because the build stripped
    /// Input.inputString; that is enough for the type-ahead search, which matches letters only.
    /// </summary>
    internal sealed class UnityNavInput : INavInput
    {
        public int FrameCount => Time.frameCount;
        public float UnscaledTime => Time.unscaledTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        public bool CtrlHeld => UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        public bool AltHeld => UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);
        public bool ShiftHeld => UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        public bool EscapeDown => UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        public bool UpHeld => UnityEngine.Input.GetKey(KeyCode.UpArrow);
        public bool DownHeld => UnityEngine.Input.GetKey(KeyCode.DownArrow);

        private readonly StringBuilder _typed = new StringBuilder(4);

        public string TypedText
        {
            get
            {
                _typed.Length = 0;
                for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
                    if (UnityEngine.Input.GetKeyDown(k))
                        _typed.Append((char)('a' + (k - KeyCode.A)));
                if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                    _typed.Append(' ');
                return _typed.Length == 0 ? "" : _typed.ToString();
            }
        }
    }
}
