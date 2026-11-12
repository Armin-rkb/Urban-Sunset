using UnityEngine;

namespace UrbanSunset
{
    /// <summary>
    /// The skater. Uses hand-written kinematic movement instead of Rigidbody2D so jumps feel the same
    /// on every device and frame rate: jump buffering, variable jump height, a double jump and a
    /// fast-fall when sliding in the air.
    /// The transform sits at the skater's feet; the ground is y = 0.
    /// </summary>
    public class Player : MonoBehaviour
    {
        // Tuning
        const float Gravity = -58f;
        const float FallGravityMultiplier = 1.3f;
        const float JumpVelocity = 16.5f;
        const float DoubleJumpVelocity = 14.5f;
        const float JumpCutMultiplier = 0.5f;
        // A quick tap always rises at least this high (clears a trash can); holding goes higher.
        const float MinJumpHeight = 1.6f;
        const float FastFallVelocity = -26f;
        const float JumpBufferTime = 0.15f;
        const float HitStunTime = 0.35f;
        const float InvulnerableTime = 1.6f;
        const float PixelsPerUnit = 32f;

        public GameManager game;

        public float VelocityY { get; private set; }
        public bool Grounded { get; private set; }
        public bool Sliding { get; private set; }
        public bool Dead { get; private set; }
        public bool Invulnerable { get { return invulnerable > 0f || Dead; } }

        Transform visual;
        SpriteRenderer sr;
        Sprite[] run, curl, hit, down;
        float animTime;
        float jumpBuffer;
        bool usedDoubleJump;
        bool jumpCut;
        float cutHeight;
        float invulnerable;
        float hitStun;
        float deathTime;
        Vector3 squash = Vector3.one;

        public Rect Hitbox
        {
            get
            {
                Vector2 p = transform.position;
                bool curled = Sliding || (usedDoubleJump && !Grounded);
                float w = curled ? 0.8f : 0.6f;
                float h = curled ? 0.85f : 1.5f;
                return new Rect(p.x - w * 0.5f, p.y + 0.05f, w, h);
            }
        }

        public void Init(GameManager gm)
        {
            game = gm;
            run = GameAssets.Frames("player_run", 5);
            curl = GameAssets.Frames("player_curl", 4);
            hit = GameAssets.Frames("player_hit", 4);
            down = GameAssets.Frames("player_down", 3);

            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            sr = visual.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;
            // Sprites are imported at 100 PPU; the original art was authored at 32 PPU.
            visual.localScale = Vector3.one * (100f / PixelsPerUnit);
            ResetState();
        }

        public void ResetState()
        {
            VelocityY = 0f;
            Grounded = true;
            Sliding = false;
            Dead = false;
            usedDoubleJump = false;
            jumpBuffer = 0f;
            invulnerable = 0f;
            hitStun = 0f;
            animTime = 0f;
            squash = Vector3.one;
            var p = transform.position;
            transform.position = new Vector3(p.x, 0f, 0f);
            sr.color = Color.white;
            sr.sprite = run[0];
        }

        /// <param name="controls">False during the countdown: the skater rolls but ignores input.</param>
        public void Tick(float dt, InputRouter input, bool controls, float worldSpeed)
        {
            if (Dead)
            {
                TickDead(dt);
                return;
            }

            invulnerable = Mathf.Max(0f, invulnerable - dt);
            hitStun = Mathf.Max(0f, hitStun - dt);

            bool jumpPressed = controls && input.JumpPressed;
            bool jumpHeld = controls && input.JumpHeld;
            bool slideHeld = controls && input.SlideHeld;

            if (jumpPressed) jumpBuffer = JumpBufferTime;
            else jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);

            // ---- Jumping ----
            if (jumpBuffer > 0f)
            {
                if (Grounded)
                {
                    VelocityY = JumpVelocity;
                    Grounded = false;
                    Sliding = false;
                    jumpCut = false;
                    cutHeight = MinJumpHeight;
                    jumpBuffer = 0f;
                    squash = new Vector3(0.75f, 1.3f, 1f);
                    game.Audio.Sfx("jump", 0.7f, 1f, 0.05f);
                    game.Dust(transform.position, 5);
                }
                else if (!usedDoubleJump)
                {
                    VelocityY = DoubleJumpVelocity;
                    usedDoubleJump = true;
                    jumpCut = false;
                    cutHeight = transform.position.y + 1.2f;
                    jumpBuffer = 0f;
                    squash = new Vector3(0.8f, 1.2f, 1f);
                    game.Audio.Sfx("jump", 0.7f, 1.3f, 0.05f);
                    game.Dust(transform.position + Vector3.up * 0.4f, 4);
                }
                // otherwise keep the buffer: the jump fires the moment we land
            }

            // Release early for a lower jump, but never lower than MinJumpHeight.
            if (!Grounded && !jumpHeld && VelocityY > 0f && !jumpCut && transform.position.y >= cutHeight)
            {
                VelocityY *= JumpCutMultiplier;
                jumpCut = true;
            }

            // ---- Sliding / fast-fall ----
            if (slideHeld)
            {
                if (Grounded)
                {
                    if (!Sliding)
                    {
                        Sliding = true;
                        squash = new Vector3(1.25f, 0.8f, 1f);
                        game.Dust(transform.position, 3);
                    }
                }
                else if (VelocityY > FastFallVelocity)
                {
                    VelocityY = FastFallVelocity;
                    jumpCut = true;
                }
            }
            else Sliding = false;

            // ---- Gravity & landing ----
            if (!Grounded)
            {
                float g = VelocityY < 0f ? Gravity * FallGravityMultiplier : Gravity;
                VelocityY += g * dt;
                Vector3 p = transform.position;
                p.y += VelocityY * dt;
                if (p.y <= 0f)
                {
                    p.y = 0f;
                    Land();
                }
                transform.position = p;
            }

            Animate(dt, worldSpeed);
        }

        void Land()
        {
            float impact = Mathf.Clamp01(-VelocityY / 30f);
            VelocityY = 0f;
            Grounded = true;
            usedDoubleJump = false;
            squash = new Vector3(1f + 0.35f * impact, 1f - 0.3f * impact, 1f);
            game.Audio.Sfx("grounded", 0.35f, 1f, 0.08f);
            game.Dust(transform.position, 4 + (int)(impact * 6));
            if (game.Input.SlideHeld) Sliding = true;
        }

        void Animate(float dt, float worldSpeed)
        {
            animTime += dt;
            Sprite s;
            if (hitStun > 0f)
            {
                s = hit[Mathf.Min(hit.Length - 1, (int)((HitStunTime - hitStun) / HitStunTime * hit.Length))];
            }
            else if (Sliding || (usedDoubleJump && !Grounded))
            {
                float fps = Sliding ? 10f + worldSpeed : 18f;
                s = curl[(int)(animTime * fps) % curl.Length];
            }
            else if (!Grounded)
            {
                s = VelocityY > 2f ? run[2] : (VelocityY > -4f ? run[3] : run[0]);
            }
            else
            {
                float fps = Mathf.Lerp(7f, 14f, Mathf.InverseLerp(7f, 17f, worldSpeed));
                s = run[(int)(animTime * fps) % run.Length];
            }
            sr.sprite = s;

            // Blink while invulnerable.
            float alpha = invulnerable > 0f && Mathf.Repeat(invulnerable, 0.16f) < 0.08f ? 0.3f : 1f;
            sr.color = new Color(1f, 1f, 1f, alpha);

            squash = Vector3.Lerp(squash, Vector3.one, 1f - Mathf.Exp(-14f * dt));
            visual.localScale = Vector3.Scale(Vector3.one * (100f / PixelsPerUnit), squash);
        }

        /// <summary>Returns true if the hit killed the skater.</summary>
        public bool TakeHit(bool lastHeart)
        {
            if (Invulnerable) return false;
            if (lastHeart)
            {
                Die();
                return true;
            }
            invulnerable = InvulnerableTime;
            hitStun = HitStunTime;
            Sliding = false;
            if (Grounded)
            {
                Grounded = false;
                VelocityY = 7f; // little knock-back hop
            }
            else VelocityY = Mathf.Max(VelocityY, 4f);
            jumpCut = true;
            return false;
        }

        void Die()
        {
            Dead = true;
            deathTime = 0f;
            Sliding = false;
            VelocityY = Mathf.Max(VelocityY, 9f);
            Grounded = false;
            sr.color = Color.white;
        }

        void TickDead(float dt)
        {
            deathTime += dt;
            Vector3 p = transform.position;
            if (!Grounded)
            {
                VelocityY += Gravity * dt;
                p.y += VelocityY * dt;
                if (p.y <= 0f)
                {
                    p.y = 0f;
                    Grounded = true;
                    VelocityY = 0f;
                    game.Dust(p, 10);
                    game.Audio.Sfx("grounded", 0.6f, 0.8f);
                }
            }
            transform.position = p;

            if (!Grounded) sr.sprite = hit[Mathf.Min(hit.Length - 1, (int)(deathTime * 10f))];
            else sr.sprite = deathTime < 0.9f ? down[0] : down[deathTime < 1.3f ? 1 : 2];
            squash = Vector3.Lerp(squash, Vector3.one, 1f - Mathf.Exp(-14f * dt));
            visual.localScale = Vector3.Scale(Vector3.one * (100f / PixelsPerUnit), squash);
        }
    }
}
