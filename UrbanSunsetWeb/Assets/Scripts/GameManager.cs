using System.Collections.Generic;
using UnityEngine;

namespace UrbanSunset
{
    /// <summary>
    /// Builds the whole game at startup and runs the game loop:
    /// title -> countdown -> playing -> dying -> game over -> (retry) countdown ...
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum State { Title, Countdown, Playing, Dying, GameOver }

        public const int MaxHearts = 3;

        // Difficulty tuning
        const float StartSpeed = 7.5f;
        const float MaxSpeed = 17f;
        const float Acceleration = 0.1f;     // units/s gained per second of play
        const float PointsPerUnit = 10f;
        const float ShieldDuration = 6f;
        const float SlowDuration = 4.5f;
        const float SlowFactor = 0.55f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            var go = new GameObject("UrbanSunset");
            DontDestroyOnLoad(go);
            go.AddComponent<GameManager>();
        }
        static bool booted;

        // Systems
        public InputRouter Input { get; private set; }
        public AudioManager Audio { get; private set; }
        Hud hud;
        WorldScroller world;
        Player player;
        Transform entityRoot;

        // Public state for the HUD
        public State CurrentState { get; private set; }
        public bool Paused { get; private set; }
        public int Score { get { return Mathf.FloorToInt(score); } }
        public int Best { get; private set; }
        public bool NewBest { get; private set; }
        public int Hearts { get; private set; }
        public float ShieldFraction { get { return shieldTime > 0f ? shieldTime / ShieldDuration : 0f; } }
        public float SlowFraction { get { return slowTime > 0f ? slowTime / SlowDuration : 0f; } }
        public bool CanRetry { get { return CurrentState == State.GameOver && stateTime > 0.7f; } }
        public bool ShowControlHints
        {
            get
            {
                return runsStarted <= 3 &&
                       (CurrentState == State.Countdown || (CurrentState == State.Playing && runTime < 3.5f));
            }
        }

        float score;
        float speed;
        float runTime;
        float stateTime;
        float spawnDistance;
        float shieldTime, slowTime, slowBlend = 1f;
        float hitStop;
        float shakeTime, shakeStrength;
        int runsStarted;
        bool shownGo;
        bool sentReady;

        readonly List<Entity> entities = new List<Entity>();
        readonly Dictionary<Entity, SpriteRenderer> warnings = new Dictionary<Entity, SpriteRenderer>();
        SpriteRenderer shieldBubble;

        // Simple pooled particles for dust and sparkles.
        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float life, maxLife, gravity;
        }
        readonly List<Particle> particles = new List<Particle>();
        readonly Stack<SpriteRenderer> particlePool = new Stack<SpriteRenderer>();

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1; // WebGL: follow the browser's refresh rate

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();

            Audio = gameObject.AddComponent<AudioManager>();
            Input = gameObject.AddComponent<InputRouter>();

            world = new GameObject("World").AddComponent<WorldScroller>();
            world.transform.SetParent(transform, false);
            world.Init(cam);

            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);

            player = new GameObject("Player").AddComponent<Player>();
            player.transform.SetParent(transform, false);
            player.Init(this);

            var bubbleGo = new GameObject("ShieldBubble");
            bubbleGo.transform.SetParent(player.transform, false);
            shieldBubble = bubbleGo.AddComponent<SpriteRenderer>();
            shieldBubble.sprite = GameAssets.Smooth("bubble");
            shieldBubble.sortingOrder = 25;
            if (shieldBubble.sprite != null)
            {
                float s = 2.3f / shieldBubble.sprite.bounds.size.y;
                bubbleGo.transform.localScale = new Vector3(s, s, 1f);
            }
            bubbleGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            shieldBubble.enabled = false;

            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Init(this);

            Input.UiClick = hud.TryClick;
            Input.GameplayZonesActive = () => !Paused && (CurrentState == State.Countdown || CurrentState == State.Playing);

            Best = PlayerPrefs.GetInt("best", 0);
            runsStarted = PlayerPrefs.GetInt("runs", 0);
            Hearts = MaxHearts;
            EnterTitle();
        }

        void EnterTitle()
        {
            SetState(State.Title);
            speed = 4f;
            Audio.PlayMusic("title", 0.5f);
        }

        void SetState(State s)
        {
            CurrentState = s;
            stateTime = 0f;
        }

        // ------------------------------------------------------------------ main loop

        void Update()
        {
            if (!sentReady)
            {
                sentReady = true;
                WebBridge.GameReady();
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            Input.Poll();
            world.Layout();

            bool canPause = CurrentState == State.Countdown || CurrentState == State.Playing;
            if (Input.PausePressed && canPause) TogglePause();

            if (!Paused) Step(dt);
            hud.Tick(dt);
        }

        void Step(float dt)
        {
            stateTime += dt;
            switch (CurrentState)
            {
                case State.Title:
                    world.Scroll(speed * dt);
                    player.Tick(dt, Input, false, speed);
                    if (Input.Tap) Restart();
                    break;

                case State.Countdown:
                    StepCountdown(dt);
                    break;

                case State.Playing:
                    StepPlaying(dt);
                    break;

                case State.Dying:
                    speed = Mathf.MoveTowards(speed, 0f, 14f * dt);
                    TickWorld(dt, dt);
                    player.Tick(dt, Input, false, speed);
                    if (stateTime > 1.7f) EnterGameOver();
                    break;

                case State.GameOver:
                    TickWorld(dt, dt);
                    player.Tick(dt, Input, false, 0f);
                    if (CanRetry && Input.Tap) Restart();
                    break;
            }
            UpdateParticles(dt);
            UpdateShake(dt);
        }

        void StepCountdown(float dt)
        {
            // READY ... GO! The skater rolls in place while the street starts moving.
            speed = Mathf.MoveTowards(speed, StartSpeed * 0.5f, 6f * dt);
            world.Scroll(speed * dt);
            player.Tick(dt, Input, false, speed);
            if (!shownGo && stateTime >= 1.0f)
            {
                shownGo = true;
                hud.ShowCountdown(GameAssets.Smooth("go"));
                Audio.Sfx("go", 0.9f);
                Audio.PlayMusic("music", 0.5f);
                SetState(State.Playing);
                runTime = 0f;
                spawnDistance = StartSpeed * 1.2f;
            }
        }

        void StepPlaying(float dt)
        {
            if (stateTime > 0.6f) hud.ShowCountdown(null);

            runTime += dt;
            float target = Mathf.Min(MaxSpeed, StartSpeed + runTime * Acceleration);
            speed = Mathf.MoveTowards(speed, target, 8f * dt);

            // Power-up timers run in real time.
            if (shieldTime > 0f) shieldTime = Mathf.Max(0f, shieldTime - dt);
            if (slowTime > 0f) slowTime = Mathf.Max(0f, slowTime - dt);
            slowBlend = Mathf.MoveTowards(slowBlend, slowTime > 0f ? SlowFactor : 1f, dt * 2f);
            Audio.SetMusicPitch(Mathf.Lerp(0.85f, 1f, (slowBlend - SlowFactor) / (1f - SlowFactor)));

            float worldDt = dt * slowBlend;
            float playerDt = dt;
            if (hitStop > 0f)
            {
                hitStop -= dt;
                worldDt = 0f;
                playerDt = 0f;
            }

            score += speed * worldDt * PointsPerUnit;
            Spawn(speed * worldDt);
            TickWorld(worldDt, playerDt);
            player.Tick(playerDt, Input, true, speed);
            CheckCollisions();
            UpdateShieldBubble();
        }

        void TickWorld(float worldDt, float realDt)
        {
            world.Scroll(speed * worldDt);
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var e = entities[i];
                e.Tick(worldDt, speed);
                bool gone = e.RightEdge < world.Left - 1f || e.transform.position.y < -8f;
                if (gone)
                {
                    RemoveEntity(i);
                    continue;
                }
                UpdateWarning(e);
            }
        }

        // ------------------------------------------------------------------ spawning

        void Spawn(float distance)
        {
            spawnDistance -= distance;
            if (spawnDistance > 0f) return;

            float x = world.Right + 1.5f;
            float length = SpawnPattern(x);

            // Gap until the next pattern, measured in seconds of travel so it stays fair at high speed.
            float tighten = Mathf.Lerp(1f, 0.7f, runTime / 100f);
            float gapSeconds = Random.Range(0.95f, 1.6f) * tighten;
            float gap = Mathf.Max(0.75f, gapSeconds) * speed;
            spawnDistance = length + gap;

            // Maybe drop a pickup in the middle of the gap.
            if (Random.value < 0.4f) SpawnPickup(x + length + gap * 0.5f);
        }

        /// <summary>Spawns one obstacle pattern starting at x. Returns its length in world units.</summary>
        float SpawnPattern(float x)
        {
            float t = runTime;
            var options = new List<KeyValuePair<int, float>>();
            options.Add(new KeyValuePair<int, float>(0, 3f));                       // trashcan
            options.Add(new KeyValuePair<int, float>(1, 2.2f));                     // low crow
            if (t > 12f) options.Add(new KeyValuePair<int, float>(2, 2f));          // duck
            if (t > 18f) options.Add(new KeyValuePair<int, float>(3, 1.2f));        // two trashcans
            if (t > 22f) options.Add(new KeyValuePair<int, float>(4, 1.5f));        // baseball
            if (t > 28f) options.Add(new KeyValuePair<int, float>(5, 1.3f));        // trashcan + high crow
            if (t > 35f) options.Add(new KeyValuePair<int, float>(6, 1.2f));        // gull
            if (t > 42f) options.Add(new KeyValuePair<int, float>(7, 1.2f));        // crow then trashcan
            if (t > 48f) options.Add(new KeyValuePair<int, float>(8, 1f));          // gull + low crow
            if (t > 55f) options.Add(new KeyValuePair<int, float>(9, 0.6f));        // Fred

            float total = 0f;
            foreach (var o in options) total += o.Value;
            float r = Random.value * total;
            int pick = 0;
            foreach (var o in options)
            {
                if (r < o.Value) { pick = o.Key; break; }
                r -= o.Value;
            }

            switch (pick)
            {
                case 0: Add(Kind.Trashcan, x); return 1f;
                case 1: Add(Kind.CrowLow, x); return 1f;
                case 2: Add(Kind.Duck, x); return 1f;
                case 3: Add(Kind.Trashcan, x); Add(Kind.Trashcan, x + 1.1f); return 2.2f;
                case 4: Add(Kind.Baseball, x + speed * 0.8f); return 1f;
                case 5: Add(Kind.Trashcan, x); Add(Kind.CrowHigh, x + 0.2f); return 1.2f;
                case 6: Add(Kind.Gull, x); return 2f;
                case 7:
                    {
                        Add(Kind.CrowLow, x);
                        float space = Mathf.Max(5f, speed * 0.65f);
                        Add(Kind.Trashcan, x + space);
                        return space + 1f;
                    }
                case 8: Add(Kind.Gull, x); Add(Kind.CrowLow, x + 0.3f); return 2f;
                default: Add(Kind.Fred, x + speed * 0.9f); return 2.5f;
            }
        }

        void SpawnPickup(float x)
        {
            Kind kind = Kind.Soda;
            float r = Random.value;
            if (Hearts < MaxHearts && r < 0.12f) kind = Kind.Heart;
            else if (runTime > 15f && r < 0.24f && shieldTime <= 0f) kind = Kind.Shield;
            else if (runTime > 20f && r < 0.34f && slowTime <= 0f) kind = Kind.Clock;
            var e = Add(kind, x);
            // Half of the pickups float high so you have to jump for them.
            e.SetHeight(Random.value < 0.5f ? 0.85f : 2.6f);
        }

        Entity Add(Kind kind, float x)
        {
            var e = Entity.Create(kind, x, entityRoot);
            entities.Add(e);
            return e;
        }

        void RemoveEntity(int index)
        {
            var e = entities[index];
            SpriteRenderer w;
            if (warnings.TryGetValue(e, out w))
            {
                Destroy(w.gameObject);
                warnings.Remove(e);
            }
            Destroy(e.gameObject);
            entities.RemoveAt(index);
        }

        /// <summary>Fast obstacles get a flashing arrow at the screen edge before they appear.</summary>
        void UpdateWarning(Entity e)
        {
            if (e.kind != Kind.Baseball && e.kind != Kind.Fred) return;
            SpriteRenderer w;
            bool offscreen = e.LeftEdge > world.Right && !e.Smashed;
            if (!warnings.TryGetValue(e, out w))
            {
                if (!offscreen) return;
                var go = new GameObject("Warning");
                go.transform.SetParent(entityRoot, false);
                w = go.AddComponent<SpriteRenderer>();
                w.sprite = GameAssets.Arrow;
                w.sortingOrder = 40;
                go.transform.localScale = Vector3.one * 0.8f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // point left
                warnings[e] = w;
            }
            w.enabled = offscreen;
            if (!offscreen) return;
            w.transform.position = new Vector3(world.Right - 0.6f, e.transform.position.y, 0f);
            bool flash = Mathf.Repeat(Time.unscaledTime, 0.24f) < 0.12f;
            w.color = flash ? new Color(1f, 0.25f, 0.15f, 1f) : new Color(1f, 0.85f, 0.2f, 1f);
        }

        // ------------------------------------------------------------------ collisions

        void CheckCollisions()
        {
            Rect pb = player.Hitbox;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var e = entities[i];
                if (e.Smashed || !pb.Overlaps(e.Hitbox)) continue;

                if (e.IsPickup)
                {
                    Collect(e);
                    RemoveEntity(i);
                    continue;
                }

                if (shieldTime > 0f)
                {
                    e.Smash(1f);
                    score += 250f;
                    hud.FloatText("+250", e.transform.position, new Color(0.55f, 0.95f, 1f));
                    Audio.Sfx("hit", 0.6f, 1.5f, 0.1f);
                    Shake(0.15f, 0.12f);
                    Sparkle(e.transform.position, new Color(0.6f, 0.95f, 1f), 10);
                    WebBridge.Vibrate(25);
                    continue;
                }

                if (player.Invulnerable) continue;

                Hearts--;
                bool dead = player.TakeHit(Hearts <= 0);
                hitStop = 0.08f;
                Shake(0.3f, dead ? 0.45f : 0.3f);
                Sparkle(player.transform.position + Vector3.up * 0.9f, new Color(1f, 0.85f, 0.3f), 12);
                WebBridge.Vibrate(dead ? 250 : 70);
                if (dead)
                {
                    Audio.StopMusic();
                    Audio.Sfx("death", 1f);
                    SetState(State.Dying);
                    shieldBubble.enabled = false;
                }
                else
                {
                    Audio.Sfx("hit", 1f, 1f, 0.05f);
                    hud.ShowToast(Hearts == 1 ? "LAST LIFE!" : "OUCH!", 1f);
                }
                break;
            }
        }

        void Collect(Entity e)
        {
            Vector3 pos = e.transform.position;
            WebBridge.Vibrate(15);
            switch (e.kind)
            {
                case Kind.Soda:
                    score += 1000f;
                    Audio.Sfx("soda", 0.8f, 1f, 0.05f);
                    hud.FloatText("+1000", pos, new Color(1f, 0.85f, 0.25f));
                    Sparkle(pos, new Color(1f, 0.6f, 0.2f), 8);
                    break;
                case Kind.Shield:
                    shieldTime = ShieldDuration;
                    Audio.Sfx("shield", 0.9f);
                    hud.FloatText("SHIELD!", pos, new Color(0.55f, 0.95f, 1f));
                    Sparkle(pos, new Color(0.6f, 0.95f, 1f), 12);
                    break;
                case Kind.Clock:
                    slowTime = SlowDuration;
                    Audio.Sfx("clock", 0.8f);
                    hud.FloatText("SLOW-MO!", pos, new Color(1f, 0.9f, 0.4f));
                    Sparkle(pos, new Color(1f, 1f, 0.6f), 12);
                    break;
                case Kind.Heart:
                    Hearts = Mathf.Min(MaxHearts, Hearts + 1);
                    Audio.Sfx("soda", 0.8f, 1.3f);
                    hud.FloatText("+1 LIFE", pos, new Color(1f, 0.4f, 0.45f));
                    Sparkle(pos, new Color(1f, 0.35f, 0.4f), 12);
                    break;
            }
        }

        void UpdateShieldBubble()
        {
            bool on = shieldTime > 0f;
            shieldBubble.enabled = on && !(shieldTime < 1.5f && Mathf.Repeat(shieldTime, 0.2f) < 0.1f);
            if (on)
            {
                float pulse = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                shieldBubble.color = new Color(1f, 1f, 1f, pulse);
                // Sit lower when the skater is curled up.
                float y = player.Sliding ? 0.5f : 0.85f;
                var lp = shieldBubble.transform.localPosition;
                shieldBubble.transform.localPosition = new Vector3(0f, Mathf.Lerp(lp.y, y, 0.3f), 0f);
            }
        }

        // ------------------------------------------------------------------ state changes

        public void Restart()
        {
            for (int i = entities.Count - 1; i >= 0; i--) RemoveEntity(i);
            foreach (var p in particles) ReleaseParticle(p.sr);
            particles.Clear();
            world.ClearLamps();
            player.ResetState();

            Paused = false;
            AudioListener.pause = false;
            Hearts = MaxHearts;
            score = 0f;
            runTime = 0f;
            shieldTime = slowTime = 0f;
            slowBlend = 1f;
            hitStop = 0f;
            NewBest = false;
            shownGo = false;
            shieldBubble.enabled = false;
            Input.ResetHeld();
            Audio.SetMusicPitch(1f);
            Audio.StopMusic();
            Audio.Sfx("ready", 0.9f);
            hud.ShowCountdown(GameAssets.Smooth("ready"));
            runsStarted++;
            PlayerPrefs.SetInt("runs", runsStarted);
            SetState(State.Countdown);
        }

        void EnterGameOver()
        {
            SetState(State.GameOver);
            Audio.Sfx("gameover", 0.9f);
            if (Score > Best)
            {
                Best = Score;
                NewBest = true;
                PlayerPrefs.SetInt("best", Best);
            }
            PlayerPrefs.Save();
        }

        public void TogglePause()
        {
            if (CurrentState != State.Countdown && CurrentState != State.Playing) return;
            Paused = !Paused;
            Input.ResetHeld();
            AudioListener.pause = Paused;
        }

        public void ShareScore()
        {
            WebBridge.Share("I scored " + Score.ToString("N0") + " in Urban Sunset! Can you beat me?");
            hud.ShowToast("SHARING...", 1.2f);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && !Paused && (CurrentState == State.Playing || CurrentState == State.Countdown)) TogglePause();
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && !Paused && (CurrentState == State.Playing || CurrentState == State.Countdown)) TogglePause();
        }

        // ------------------------------------------------------------------ juice

        void Shake(float duration, float strength)
        {
            shakeTime = Mathf.Max(shakeTime, duration);
            shakeStrength = Mathf.Max(shakeStrength, strength);
        }

        void UpdateShake(float dt)
        {
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                float k = Mathf.Clamp01(shakeTime / 0.3f);
                world.SetShake(Random.insideUnitCircle * shakeStrength * k);
                if (shakeTime <= 0f)
                {
                    shakeStrength = 0f;
                    world.SetShake(Vector2.zero);
                }
            }
        }

        /// <summary>Little dust puffs kicked up behind the skates.</summary>
        public void Dust(Vector3 pos, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float shade = Random.Range(0.7f, 0.95f);
                Emit(pos + new Vector3(Random.Range(-0.3f, 0.2f), 0.05f, 0f),
                     new Vector2(-Random.Range(1.5f, 4f), Random.Range(0.5f, 2.5f)),
                     new Color(shade, shade * 0.85f, shade * 0.8f, 0.9f),
                     Random.Range(0.08f, 0.16f), Random.Range(0.25f, 0.45f), 6f);
            }
        }

        void Sparkle(Vector3 pos, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Emit(pos, dir * Random.Range(3f, 7f), color, Random.Range(0.1f, 0.18f), Random.Range(0.3f, 0.55f), 4f);
            }
        }

        void Emit(Vector3 pos, Vector2 velocity, Color color, float size, float life, float gravity)
        {
            SpriteRenderer sr = particlePool.Count > 0 ? particlePool.Pop() : null;
            if (sr == null)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GameAssets.White;
                sr.sortingOrder = 35;
            }
            sr.gameObject.SetActive(true);
            sr.color = color;
            sr.transform.position = pos;
            sr.transform.localScale = new Vector3(size, size, 1f);
            particles.Add(new Particle { sr = sr, velocity = velocity, life = life, maxLife = life, gravity = gravity });
        }

        void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    ReleaseParticle(p.sr);
                    particles.RemoveAt(i);
                    continue;
                }
                p.velocity.y -= p.gravity * dt;
                p.sr.transform.position += (Vector3)(p.velocity * dt);
                var c = p.sr.color;
                c.a = p.life / p.maxLife;
                p.sr.color = c;
            }
        }

        void ReleaseParticle(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            particlePool.Push(sr);
        }
    }
}
