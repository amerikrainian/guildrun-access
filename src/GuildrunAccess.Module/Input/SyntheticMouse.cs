using System;
using GuildrunAccess.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GuildrunAccess.Module.Input
{
    /// <summary>
    /// A mouse click the game cannot tell from a real one: queued into Unity's Input System as mouse
    /// state events (move, press, release), so it reaches uGUI's input module AND the game's own input
    /// service, which polls "was the pointer pressed this frame" for its click-anywhere prompts (the
    /// comics, the run-over panel). Use it only where a widget's own event (<c>onClick.Invoke</c>) is
    /// not what the game listens to; the OS cursor is left alone.
    /// </summary>
    internal static class SyntheticMouse
    {
        /// <summary>Click at a screen position (Unity screen space: origin bottom-left, pixels).</summary>
        public static bool Click(Vector2 screenPosition)
        {
            try
            {
                var mouse = Mouse.current;
                if (mouse == null)
                {
                    CoreLog.Warning("SyntheticMouse: no mouse device");
                    return false;
                }
                Queue(mouse, screenPosition, 0);
                Queue(mouse, screenPosition, 1);
                Queue(mouse, screenPosition, 0);
                return true;
            }
            catch (Exception e)
            {
                CoreLog.Warning("SyntheticMouse: click failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Click the middle of a UI element (its RectTransform's position on screen).</summary>
        public static bool Click(Component target)
        {
            var position = ScreenPosition(target);
            return position.HasValue && Click(position.Value);
        }

        /// <summary>Click the middle of the screen (a click-anywhere prompt).</summary>
        public static bool ClickCenter() => Click(new Vector2(Screen.width / 2f, Screen.height / 2f));

        /// <summary>Where a UI element sits on screen, through its canvas's camera; null when it has no RectTransform.</summary>
        public static Vector2? ScreenPosition(Component target)
        {
            try
            {
                var rect = target != null ? target.GetComponent<RectTransform>() : null;
                if (rect == null) return null;
                var canvas = target.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            }
            catch (Exception e)
            {
                CoreLog.Warning("SyntheticMouse: position failed: " + e.Message);
                return null;
            }
        }

        private static void Queue(Mouse mouse, Vector2 position, ushort buttons)
        {
            var state = new MouseState();
            state.position = position;
            state.buttons = buttons;
            InputSystem.QueueStateEvent<MouseState>(mouse, state, -1);
        }
    }
}
