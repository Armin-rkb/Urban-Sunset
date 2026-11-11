using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UrbanSunset
{
    /// <summary>
    /// All on-screen UI, built in code with uGUI: score, lives, power-up timers, the touch buttons,
    /// and the title / pause / game-over screens. Taps are hit-tested here directly (no EventSystem)
    /// so the UI and the gameplay touch zones never fight over the same finger.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        class UiButton
        {
            public RectTransform rt;
            public System.Action onClick;
            public float pop;
        }

        class Floater
        {
            public Text text;
            public Vector3 world;
            public float t;
        }

        static readonly Color Gold = new Color32(255, 214, 64, 255);
        static readonly Color Cyan = new Color32(90, 230, 255, 255);

        GameManager game;
        CanvasScaler scaler;
        RectTransform canvasRect, safe;
        Rect lastSafeArea;
        Vector2Int lastScreen;

        readonly List<UiButton> buttons = new List<UiButton>();
        readonly List<Floater> floaters = new List<Floater>();

        // HUD
        RectTransform hudRoot;
        Text scoreText, bestText;
        Image[] hearts;
        RectTransform shieldTimer, slowTimer;
        Image shieldFill, slowFill;
        UiButton pauseButton, muteButton;
        Image muteIcon;

        // Touch controls
        RectTransform touchRoot, jumpPad, slidePad;
        Image jumpBg, slideBg;
        Text jumpHint, slideHint;
        float hintAlpha;

        // Screens
        RectTransform titleRoot, pauseRoot, overRoot;
        RectTransform logo;
        Text tapToPlay, titleBest, controlsText;
        Image countdownImage;
        float countdownPop;
        Text overScore, overBest, overNewBest, overRetryHint;
        UiButton retryButton, shareButton, resumeButton, restartButton;
        Text toast;
        float toastTime;
        float time;

        public void Init(GameManager gm)
        {
            game = gm;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = false;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasRect = canvasGo.GetComponent<RectTransform>();

            safe = NewRect("SafeArea", canvasRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            BuildTouchControls();
            BuildHud();
            BuildTitle();
            BuildPause();
            BuildGameOver();
            BuildMuteButton();

            countdownImage = NewImage("Countdown", safe, null, Color.white);
            SetAnchors(countdownImage.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(420f, 200f));
            countdownImage.preserveAspect = true;
            countdownImage.gameObject.SetActive(false);

            toast = NewText("Toast", safe, "", 18, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(toast.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(1000f, 60f));

            ApplyLayout();
        }

        // ------------------------------------------------------------------ building

        void BuildHud()
        {
            hudRoot = NewRect("Hud", safe, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            hearts = new Image[GameManager.MaxHearts];
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i] = NewImage("Heart" + i, hudRoot, GameAssets.Pixel("heart"), Color.white);
                SetAnchors(hearts[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f + i * 62f, -50f), new Vector2(52f, 48f));
                hearts[i].preserveAspect = true;
            }

            shieldTimer = MakeTimer("ShieldTimer", GameAssets.Smooth("bubble"), Cyan, new Vector2(52f, -112f), out shieldFill);
            slowTimer = MakeTimer("SlowTimer", GameAssets.Pixel("clock"), Gold, new Vector2(116f, -112f), out slowFill);

            scoreText = NewText("Score", hudRoot, "0", 36, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 50f));
            bestText = NewText("Best", hudRoot, "", 14, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            SetAnchors(bestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(600f, 24f));

            pauseButton = MakeIconButton("Pause", new Vector2(1f, 1f), new Vector2(-56f, -54f), 84f, () => game.TogglePause());
            for (int i = 0; i < 2; i++)
            {
                var bar = NewImage("Bar", pauseButton.rt, GameAssets.White, Color.white);
                SetAnchors(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -10f : 10f, 0f), new Vector2(10f, 32f));
            }

        }

        void BuildMuteButton()
        {
            // Lives on the safe-area root (not the HUD) so it is also available on the title screen.
            muteButton = MakeIconButton("Mute", new Vector2(1f, 1f), new Vector2(-152f, -54f), 84f, () =>
            {
                game.Audio.ToggleMute();
                RefreshMuteIcon();
            }, safe);
            muteIcon = NewImage("Icon", muteButton.rt, GameAssets.Speaker(true), Color.white);
            SetAnchors(muteIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            RefreshMuteIcon();
        }

        RectTransform MakeTimer(string name, Sprite icon, Color color, Vector2 pos, out Image fill)
        {
            var root = NewRect(name, hudRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), pos, new Vector2(56f, 56f));
            var bg = NewImage("Bg", root, GameAssets.Circle, new Color(0f, 0f, 0f, 0.45f));
            Stretch(bg.rectTransform);
            fill = NewImage("Fill", root, GameAssets.Ring, color);
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            var ic = NewImage("Icon", root, icon, Color.white);
            SetAnchors(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            ic.preserveAspect = true;
            root.gameObject.SetActive(false);
            return root;
        }

        void BuildTouchControls()
        {
            touchRoot = NewRect("TouchControls", safe, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            slidePad = MakePad("SlidePad", new Vector2(0f, 0f), new Vector2(150f, 150f), 180f, out slideBg);
            jumpPad = MakePad("JumpPad", new Vector2(1f, 0f), new Vector2(-150f, 150f), 0f, out jumpBg);

            slideHint = NewText("SlideHint", touchRoot, "HOLD LEFT SIDE\nTO SLIDE", 14, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(slideHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 290f), new Vector2(360f, 60f));
            jumpHint = NewText("JumpHint", touchRoot, "TAP RIGHT SIDE TO JUMP\nTAP AGAIN TO DOUBLE JUMP", 14, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(jumpHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-230f, 290f), new Vector2(480f, 60f));
        }

        RectTransform MakePad(string name, Vector2 anchor, Vector2 pos, float arrowRotation, out Image bg)
        {
            var root = NewRect(name, touchRoot, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(190f, 190f));
            bg = NewImage("Bg", root, GameAssets.Circle, new Color(1f, 1f, 1f, 0.12f));
            Stretch(bg.rectTransform);
            var ring = NewImage("Ring", root, GameAssets.Ring, new Color(1f, 1f, 1f, 0.55f));
            Stretch(ring.rectTransform);
            var arrow = NewImage("Arrow", root, GameAssets.Arrow, new Color(1f, 1f, 1f, 0.85f));
            SetAnchors(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, arrowRotation);
            return root;
        }

        void BuildTitle()
        {
            titleRoot = NewRect("Title", safe, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shade = NewImage("Shade", titleRoot, GameAssets.White, new Color(0f, 0f, 0f, 0.25f));
            Stretch(shade.rectTransform, 2000f);

            var logoImg = NewImage("Logo", titleRoot, GameAssets.Smooth("logo"), Color.white);
            logoImg.preserveAspect = true;
            logo = logoImg.rectTransform;
            SetAnchors(logo, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(400f, 400f));

            tapToPlay = NewText("TapToPlay", titleRoot, "TAP TO PLAY", 28, Gold, TextAnchor.MiddleCenter);
            SetAnchors(tapToPlay.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(900f, 50f));
            titleBest = NewText("Best", titleRoot, "", 16, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(titleBest.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0f, -50f), new Vector2(900f, 30f));
            controlsText = NewText("Controls", titleRoot, "", 12, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            SetAnchors(controlsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1200f, 40f));
        }

        void BuildPause()
        {
            pauseRoot = NewRect("Pause", safe, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shade = NewImage("Shade", pauseRoot, GameAssets.White, new Color(0f, 0f, 0f, 0.6f));
            Stretch(shade.rectTransform, 2000f);
            var title = NewText("Paused", pauseRoot, "PAUSED", 48, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(title.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(900f, 80f));
            resumeButton = MakeTextButton("Resume", pauseRoot, "RESUME", new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(360f, 96f), Gold, () => game.TogglePause());
            restartButton = MakeTextButton("Restart", pauseRoot, "RESTART", new Vector2(0.5f, 0.45f), new Vector2(0f, -120f), new Vector2(360f, 80f), Color.white, () => game.Restart());
            pauseRoot.gameObject.SetActive(false);
        }

        void BuildGameOver()
        {
            overRoot = NewRect("GameOver", safe, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shade = NewImage("Shade", overRoot, GameAssets.White, new Color(0f, 0f, 0f, 0.5f));
            Stretch(shade.rectTransform, 2000f);
            var img = NewImage("GameOverImage", overRoot, GameAssets.Smooth("gameover"), Color.white);
            img.preserveAspect = true;
            SetAnchors(img.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(620f, 140f));

            overScore = NewText("Score", overRoot, "", 40, Color.white, TextAnchor.MiddleCenter);
            SetAnchors(overScore.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(900f, 60f));
            overBest = NewText("Best", overRoot, "", 16, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            SetAnchors(overBest.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), new Vector2(0f, -50f), new Vector2(900f, 30f));
            overNewBest = NewText("NewBest", overRoot, "NEW BEST!", 20, Gold, TextAnchor.MiddleCenter);
            SetAnchors(overNewBest.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), new Vector2(0f, 50f), new Vector2(900f, 30f));

            retryButton = MakeTextButton("Retry", overRoot, "RETRY", new Vector2(0.5f, 0.3f), new Vector2(-200f, 0f), new Vector2(340f, 100f), Gold, () => game.Restart());
            shareButton = MakeTextButton("Share", overRoot, "SHARE", new Vector2(0.5f, 0.3f), new Vector2(200f, 0f), new Vector2(340f, 100f), Cyan, () => game.ShareScore());
            overRetryHint = NewText("Hint", overRoot, "", 12, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            SetAnchors(overRetryHint.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(0f, -86f), new Vector2(900f, 30f));
            overRoot.gameObject.SetActive(false);
        }

        UiButton MakeIconButton(string name, Vector2 anchor, Vector2 pos, float size, System.Action onClick, RectTransform parent = null)
        {
            var rt = NewRect(name, parent != null ? parent : hudRoot, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var bg = NewImage("Bg", rt, GameAssets.Circle, new Color(0f, 0f, 0f, 0.4f));
            Stretch(bg.rectTransform);
            var ring = NewImage("Ring", rt, GameAssets.Ring, new Color(1f, 1f, 1f, 0.7f));
            Stretch(ring.rectTransform);
            var b = new UiButton { rt = rt, onClick = onClick };
            buttons.Add(b);
            return b;
        }

        UiButton MakeTextButton(string name, RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, System.Action onClick)
        {
            var rt = NewRect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var bg = NewImage("Bg", rt, GameAssets.White, new Color(0f, 0f, 0f, 0.55f));
            Stretch(bg.rectTransform);
            var top = NewImage("Accent", rt, GameAssets.White, color);
            SetAnchors(top.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(0f, 6f));
            var text = NewText("Label", rt, label, 26, color, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            var b = new UiButton { rt = rt, onClick = onClick };
            buttons.Add(b);
            return b;
        }

        // ------------------------------------------------------------------ input

        /// <summary>Hit-tests a screen position against visible buttons. Fingers get a generous margin.</summary>
        public bool TryClick(Vector2 screenPos)
        {
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                var b = buttons[i];
                if (!b.rt.gameObject.activeInHierarchy) continue;
                if (!Contains(b.rt, screenPos, 14f)) continue;
                b.pop = 1f;
                b.onClick();
                return true;
            }
            return false;
        }

        bool Contains(RectTransform rt, Vector2 screenPos, float marginRef)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out local)) return false;
            Rect r = rt.rect;
            r.xMin -= marginRef; r.xMax += marginRef; r.yMin -= marginRef; r.yMax += marginRef;
            return r.Contains(local);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float dt)
        {
            time += dt;
            ApplyLayout();

            var state = game.CurrentState;
            bool playing = state == GameManager.State.Playing || state == GameManager.State.Countdown;

            titleRoot.gameObject.SetActive(state == GameManager.State.Title);
            overRoot.gameObject.SetActive(state == GameManager.State.GameOver);
            pauseRoot.gameObject.SetActive(game.Paused);
            hudRoot.gameObject.SetActive(state != GameManager.State.Title);
            pauseButton.rt.gameObject.SetActive(playing && !game.Paused);
            muteButton.rt.gameObject.SetActive(state != GameManager.State.Dying);
            touchRoot.gameObject.SetActive(game.Input.TouchMode && playing && !game.Paused);

            // Score & lives
            scoreText.text = game.Score.ToString("N0");
            bestText.text = "BEST " + Mathf.Max(game.Best, game.Score).ToString("N0");
            for (int i = 0; i < hearts.Length; i++)
            {
                bool full = i < game.Hearts;
                hearts[i].color = full ? Color.white : new Color(0.15f, 0.15f, 0.2f, 0.6f);
                float beat = full && game.Hearts == 1 ? 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(time * 8f)) : 1f;
                hearts[i].rectTransform.localScale = Vector3.one * beat;
            }

            UpdateTimer(shieldTimer, shieldFill, game.ShieldFraction);
            UpdateTimer(slowTimer, slowFill, game.SlowFraction);

            // Touch pads light up while held.
            UpdatePad(jumpPad, jumpBg, game.Input.JumpHeld);
            UpdatePad(slidePad, slideBg, game.Input.SlideHeld);
            float targetHint = game.ShowControlHints ? 1f : 0f;
            hintAlpha = Mathf.MoveTowards(hintAlpha, targetHint, dt * 2f);
            SetAlpha(jumpHint, hintAlpha);
            SetAlpha(slideHint, hintAlpha);

            // Title
            if (state == GameManager.State.Title)
            {
                logo.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(time * 2.2f));
                SetAlpha(tapToPlay, 0.55f + 0.45f * Mathf.Sin(time * 4f));
                bool touch = game.Input.TouchMode;
                tapToPlay.text = touch ? "TAP TO PLAY" : "CLICK OR PRESS SPACE TO PLAY";
                titleBest.text = game.Best > 0 ? "BEST  " + game.Best.ToString("N0") : "";
                controlsText.text = touch
                    ? "RIGHT SIDE: JUMP / DOUBLE JUMP    LEFT SIDE: SLIDE    SWIPE DOWN: FAST FALL"
                    : "SPACE / UP: JUMP (TWICE TO DOUBLE JUMP)    DOWN / S: SLIDE    P: PAUSE";
            }

            // Game over
            if (state == GameManager.State.GameOver)
            {
                overScore.text = game.Score.ToString("N0");
                overBest.text = "BEST  " + game.Best.ToString("N0");
                overNewBest.gameObject.SetActive(game.NewBest);
                if (game.NewBest) overNewBest.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 8f));
                bool ready = game.CanRetry;
                retryButton.rt.gameObject.SetActive(ready);
                shareButton.rt.gameObject.SetActive(ready);
                overRetryHint.text = ready ? (game.Input.TouchMode ? "OR TAP ANYWHERE" : "OR PRESS SPACE") : "";
            }

            // Countdown pop
            if (countdownImage.gameObject.activeSelf)
            {
                countdownPop = Mathf.MoveTowards(countdownPop, 0f, dt * 4f);
                countdownImage.rectTransform.localScale = Vector3.one * (1f + countdownPop * countdownPop * 0.6f);
            }

            // Button press feedback
            foreach (var b in buttons)
            {
                b.pop = Mathf.MoveTowards(b.pop, 0f, dt * 6f);
                b.rt.localScale = Vector3.one * (1f - 0.1f * b.pop);
            }

            // Toast
            toastTime -= dt;
            SetAlpha(toast, Mathf.Clamp01(toastTime * 2f));

            // Floating score texts
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.t += dt;
                f.world += Vector3.up * dt * 1.6f;
                PlaceAtWorld(f.text.rectTransform, f.world);
                SetAlpha(f.text, 1f - Mathf.Clamp01((f.t - 0.5f) / 0.4f));
                f.text.rectTransform.localScale = Vector3.one * (1f + 0.4f * Mathf.Max(0f, 0.15f - f.t) / 0.15f);
                if (f.t > 0.9f)
                {
                    Destroy(f.text.gameObject);
                    floaters.RemoveAt(i);
                }
            }
        }

        void UpdateTimer(RectTransform root, Image fill, float fraction)
        {
            bool on = fraction > 0f;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) return;
            fill.fillAmount = fraction;
            bool blink = fraction < 0.25f && Mathf.Repeat(time, 0.3f) < 0.15f;
            root.localScale = Vector3.one * (blink ? 0.9f : 1f);
        }

        void UpdatePad(RectTransform pad, Image bg, bool held)
        {
            float target = held ? 0.88f : 1f;
            float s = Mathf.Lerp(pad.localScale.x, target, 0.5f);
            pad.localScale = new Vector3(s, s, 1f);
            bg.color = new Color(1f, 1f, 1f, held ? 0.38f : 0.12f);
        }

        public void ShowCountdown(Sprite sprite)
        {
            if (sprite == null)
            {
                countdownImage.gameObject.SetActive(false);
                return;
            }
            countdownImage.sprite = sprite;
            countdownImage.gameObject.SetActive(true);
            countdownPop = 1f;
        }

        public void ShowToast(string message, float seconds = 2f)
        {
            toast.text = message;
            toastTime = seconds;
        }

        public void FloatText(string message, Vector3 worldPos, Color color)
        {
            var t = NewText("Float", safe, message, 22, color, TextAnchor.MiddleCenter);
            t.rectTransform.sizeDelta = new Vector2(400f, 40f);
            var f = new Floater { text = t, world = worldPos };
            PlaceAtWorld(t.rectTransform, worldPos);
            floaters.Add(f);
        }

        void PlaceAtWorld(RectTransform rt, Vector3 world)
        {
            Vector2 screen = Camera.main.WorldToScreenPoint(world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local;
        }

        public void RefreshMuteIcon()
        {
            muteIcon.sprite = GameAssets.Speaker(!game.Audio.Muted);
        }

        // ------------------------------------------------------------------ layout helpers

        void ApplyLayout()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            Rect sa = Screen.safeArea;
            if (screen == lastScreen && sa == lastSafeArea) return;
            lastScreen = screen;
            lastSafeArea = sa;

            bool landscape = Screen.width >= Screen.height;
            // Landscape: keep UI sized by height. Portrait: size by width so nothing runs off screen.
            scaler.matchWidthOrHeight = landscape ? 1f : 0f;
            scaler.referenceResolution = landscape ? new Vector2(1280f, 720f) : new Vector2(1000f, 1280f);

            if (screen.x > 0 && screen.y > 0)
            {
                safe.anchorMin = new Vector2(sa.xMin / screen.x, sa.yMin / screen.y);
                safe.anchorMax = new Vector2(sa.xMax / screen.x, sa.yMax / screen.y);
                safe.offsetMin = safe.offsetMax = Vector2.zero;
            }
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float bleed = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-bleed, -bleed);
            rt.offsetMax = new Vector2(bleed, bleed);
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string name, Transform parent, string value, int size, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = GameAssets.Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = value;
            t.lineSpacing = 1.4f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
