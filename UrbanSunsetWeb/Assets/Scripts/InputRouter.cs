using System.Collections.Generic;
using UnityEngine;

namespace UrbanSunset
{
    /// <summary>
    /// Turns touch, mouse and keyboard into three game actions: Jump, Slide and "Tap".
    ///
    /// Touch layout (landscape or portrait):
    ///   - left 40% of the screen  = SLIDE zone (hold to slide, hold in the air to fast-fall)
    ///   - right 60% of the screen = JUMP zone  (tap to jump, tap again in the air to double jump,
    ///                                            hold for a higher jump)
    ///   - swipe down anywhere in the jump zone turns that finger into a slide
    ///   - swipe up in the slide zone triggers a jump
    /// Input fires on touch-down, never on release, so there is no added latency.
    /// Every finger is tracked separately, so you can hold slide with one thumb and jump with the other.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public const float SlideZoneWidth = 0.4f;

        enum Zone { None, Jump, Slide, Ui }

        class Finger
        {
            public Zone zone;
            public Vector2 start;
            public bool swiped;
        }

        readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        const int MouseId = -100;
        float lastTouchTime = -10f;

        // Results for the current frame
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SlideHeld { get; private set; }
        public bool Tap { get; private set; }
        public bool PausePressed { get; private set; }
        public bool TouchMode { get; private set; }

        /// <summary>Returns true when a screen position hits a HUD button (and performs its action).</summary>
        public System.Func<Vector2, bool> UiClick;
        /// <summary>Returns true when touch controls should consume touches as jump/slide (during play).</summary>
        public System.Func<bool> GameplayZonesActive;

        void Awake()
        {
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;
            TouchMode = WebBridge.IsTouchDevice();
        }

        /// <summary>Called by the GameManager at the start of every frame.</summary>
        public void Poll()
        {
            JumpPressed = false;
            Tap = false;
            PausePressed = false;

            bool zones = GameplayZonesActive == null || GameplayZonesActive();

            // ---- Touch ----
            int touchCount = Input.touchCount;
            if (touchCount > 0)
            {
                TouchMode = true;
                lastTouchTime = Time.unscaledTime;
            }
            for (int i = 0; i < touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                HandlePointer(t.fingerId, t.position, t.phase, zones);
            }

            // ---- Mouse (desktop). Ignored right after a touch so browser-synthesised clicks
            // can't trigger a second jump. ----
            if (touchCount == 0 && Time.unscaledTime - lastTouchTime > 1f)
            {
                Vector2 mp = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) HandlePointer(MouseId, mp, TouchPhase.Began, zones);
                else if (Input.GetMouseButton(0) && fingers.ContainsKey(MouseId)) HandlePointer(MouseId, mp, TouchPhase.Moved, zones);
                else if (Input.GetMouseButtonUp(0) && fingers.ContainsKey(MouseId)) HandlePointer(MouseId, mp, TouchPhase.Ended, zones);
            }

            // Drop fingers that vanished without an Ended event (e.g. the tab lost focus).
            if (touchCount == 0 && !Input.GetMouseButton(0) && fingers.Count > 0 && !Input.GetMouseButtonUp(0)) fingers.Clear();

            bool jumpHeld = false, slideHeld = false;
            foreach (var f in fingers.Values)
            {
                if (f.zone == Zone.Jump) jumpHeld = true;
                if (f.zone == Zone.Slide) slideHeld = true;
            }

            // ---- Keyboard ----
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                JumpPressed = true;
                Tap = true;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Tap = true;
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) jumpHeld = true;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.LeftShift))
                slideHeld = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) PausePressed = true;

            JumpHeld = jumpHeld;
            SlideHeld = slideHeld;
        }

        void HandlePointer(int id, Vector2 pos, TouchPhase phase, bool zones)
        {
            Finger f;
            switch (phase)
            {
                case TouchPhase.Began:
                    f = new Finger { start = pos };
                    if (UiClick != null && UiClick(pos))
                    {
                        f.zone = Zone.Ui;
                    }
                    else
                    {
                        Tap = true;
                        if (zones)
                        {
                            f.zone = pos.x < Screen.width * SlideZoneWidth ? Zone.Slide : Zone.Jump;
                            if (f.zone == Zone.Jump) JumpPressed = true;
                        }
                        else f.zone = Zone.None;
                    }
                    fingers[id] = f;
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (!fingers.TryGetValue(id, out f) || f.swiped || !zones) break;
                    float threshold = Mathf.Max(30f, Screen.height * 0.07f);
                    float dy = pos.y - f.start.y;
                    if (f.zone == Zone.Jump && dy < -threshold)
                    {
                        f.zone = Zone.Slide; // swipe down = slide / fast-fall
                        f.swiped = true;
                    }
                    else if (f.zone == Zone.Slide && dy > threshold)
                    {
                        f.zone = Zone.Jump; // swipe up = jump
                        f.swiped = true;
                        JumpPressed = true;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    fingers.Remove(id);
                    break;
            }
        }

        /// <summary>Forget all held fingers (used when pausing or switching screens).</summary>
        public void ResetHeld()
        {
            foreach (var f in fingers.Values)
                if (f.zone == Zone.Jump || f.zone == Zone.Slide) f.zone = Zone.None;
        }
    }
}
