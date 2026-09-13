using System;
using System.Collections.Generic;
using GuildrunAccess.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GuildrunAccess.Module.Input
{
    /// <summary>
    /// A mouse click the game cannot tell from a real one: queued into Unity's Input System as mouse
    /// state events, so it reaches uGUI's input module AND the game's own input service, which polls
    /// "was the pointer pressed this frame" over a hovered widget for its click-anywhere prompts (the
    /// comics, the run-over panel's Proceed). The move, the press and the release go out on THREE
    /// CONSECUTIVE FRAMES (<see cref="Tick"/>): queued together they land in one input update, and the
    /// game's poll then sees a press on a widget the pointer only reached that same frame, before its
    /// hover state exists, and misses the click about half the time. Use it only where a widget's own
    /// event (<c>onClick.Invoke</c>) is not what the game listens to; the OS cursor is left alone.
    /// </summary>
    internal static class SyntheticMouse
    {
        private struct Step { public Vector2 Position; public ushort Buttons; }
        private static readonly Queue<Step> _steps = new Queue<Step>();
        private static Step _last; // the state the device was last put in (a press must get its release)

        /// <summary>Click at a screen position (Unity screen space: origin bottom-left, pixels): the
        /// pointer moves there on the next tick, presses the tick after, releases the one after that.</summary>
        public static bool Click(Vector2 screenPosition)
        {
            if (Mouse.current == null)
            {
                CoreLog.Warning("SyntheticMouse: no mouse device");
                return false;
            }
            _steps.Enqueue(new Step { Position = screenPosition, Buttons = 0 });
            _steps.Enqueue(new Step { Position = screenPosition, Buttons = 1 });
            _steps.Enqueue(new Step { Position = screenPosition, Buttons = 0 });
            return true;
        }

        /// <summary>Click the middle of a UI element (its RectTransform's position on screen).</summary>
        public static bool Click(Component target)
        {
            var position = ScreenPosition(target);
            return position.HasValue && Click(position.Value);
        }

        /// <summary>Click the middle of the screen (a click-anywhere prompt).</summary>
        public static bool ClickCenter() => Click(new Vector2(Screen.width / 2f, Screen.height / 2f));

        /// <summary>One step of the pending clicks per frame, from the module tick.</summary>
        public static void Tick()
        {
            if (_steps.Count == 0) return;
            Send(_steps.Dequeue());
        }

        /// <summary>Drop pending clicks; a press already sent gets its release now, so a module going
        /// away (a reload, a shutdown) never leaves the button held.</summary>
        public static void Reset()
        {
            _steps.Clear();
            if (_last.Buttons != 0) Send(new Step { Position = _last.Position, Buttons = 0 });
        }

        private static void Send(Step step)
        {
            try
            {
                var mouse = Mouse.current;
                if (mouse == null)
                {
                    CoreLog.Warning("SyntheticMouse: mouse device gone mid-click");
                    _steps.Clear();
                    _last = default;
                    return;
                }
                var state = new MouseState();
                state.position = step.Position;
                state.buttons = step.Buttons;
                InputSystem.QueueStateEvent<MouseState>(mouse, state, -1);
                _last = step;
            }
            catch (Exception e)
            {
                CoreLog.Warning("SyntheticMouse: click step failed: " + e.Message);
                _steps.Clear();
            }
        }

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
    }
}
