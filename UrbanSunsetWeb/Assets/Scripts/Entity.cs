using UnityEngine;

namespace UrbanSunset
{
    public enum Kind
    {
        // Obstacles
        Trashcan,   // ground, jump over
        Duck,       // ground, waddles toward you, jump over
        CrowLow,    // head height, slide under (or jump high)
        CrowHigh,   // high up, punishes double jumps
        Baseball,   // fast, head height, slide under
        Gull,       // big bird gliding high, don't jump into it
        Fred,       // rare flaming head, big, jump over
        // Pickups
        Soda,       // +1000 points
        Shield,     // smash through obstacles for a few seconds
        Clock,      // slow motion
        Heart       // +1 life
    }

    /// <summary>One obstacle or pickup scrolling toward the player.</summary>
    public class Entity : MonoBehaviour
    {
        public Kind kind;
        public bool IsPickup { get { return kind >= Kind.Soda; } }
        public bool Smashed { get; private set; }
        /// <summary>Extra speed on top of the world speed (things that move toward you).</summary>
        public float extraSpeed;
        public bool scored;

        SpriteRenderer sr;
        Sprite[] frames;
        float fps;
        float t;
        float baseY;
        float bob, bobSpeed;
        float spin;
        Vector2 hitSize;
        Vector2 hitOffset;
        Vector2 smashVelocity;

        public Rect Hitbox
        {
            get
            {
                Vector2 c = (Vector2)transform.position + hitOffset;
                return new Rect(c.x - hitSize.x * 0.5f, c.y - hitSize.y * 0.5f, hitSize.x, hitSize.y);
            }
        }

        public float RightEdge { get { return sr.bounds.max.x; } }
        public float LeftEdge { get { return sr.bounds.min.x; } }

        public static Entity Create(Kind kind, float x, Transform parent)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<Entity>();
            e.Setup(kind, x);
            return e;
        }

        void Setup(Kind k, float x)
        {
            kind = k;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = IsPickup ? 15 : 10;
            fps = 10f;
            float height;   // world height of the sprite
            float y;        // world y of the sprite's centre
            switch (k)
            {
                case Kind.Trashcan:
                    frames = GameAssets.Frames("trashcan", 4);
                    height = 1.35f; y = height * 0.5f; fps = 8f;
                    hitSize = new Vector2(0.62f, 1.05f); hitOffset = new Vector2(0f, -0.12f);
                    break;
                case Kind.Duck:
                    frames = new[] { GameAssets.Pixel("duck_1"), GameAssets.Pixel("duck_2"), GameAssets.Pixel("duck_3"),
                                     GameAssets.Pixel("duck_4"), GameAssets.Pixel("duck_5"), GameAssets.Pixel("duck_6") };
                    height = 1.0f; y = height * 0.5f; fps = 12f;
                    extraSpeed = 2.2f;
                    hitSize = new Vector2(0.65f, 0.8f); hitOffset = new Vector2(0.05f, -0.08f);
                    break;
                case Kind.CrowLow:
                    frames = GameAssets.Frames("crow", 6);
                    height = 0.75f; y = 1.42f; fps = 14f;
                    extraSpeed = 1f;
                    hitSize = new Vector2(0.95f, 0.42f); hitOffset = Vector2.zero;
                    bob = 0.06f; bobSpeed = 9f;
                    break;
                case Kind.CrowHigh:
                    frames = GameAssets.Frames("crow", 6);
                    height = 0.75f; y = 4.35f; fps = 14f; // only a double jump reaches it
                    extraSpeed = 1f;
                    hitSize = new Vector2(0.95f, 0.42f); hitOffset = Vector2.zero;
                    bob = 0.12f; bobSpeed = 6f;
                    break;
                case Kind.Baseball:
                    frames = new[] { GameAssets.Pixel("baseball") };
                    height = 0.5f; y = 1.3f;
                    extraSpeed = 7f; spin = 900f;
                    hitSize = new Vector2(0.4f, 0.4f); hitOffset = Vector2.zero;
                    break;
                case Kind.Gull:
                    frames = new[] { GameAssets.Pixel("bird") };
                    height = 1.5f; y = 2.75f;
                    extraSpeed = 1.5f;
                    hitSize = new Vector2(1.2f, 0.6f); hitOffset = new Vector2(0f, 0.05f);
                    bob = 0.25f; bobSpeed = 3f;
                    break;
                case Kind.Fred:
                    frames = new[] { GameAssets.Pixel("fred") };
                    height = 1.3f; y = 0.75f; fps = 0f;
                    extraSpeed = 3.5f;
                    hitSize = new Vector2(1.0f, 1.0f); hitOffset = new Vector2(-0.15f, -0.05f);
                    bob = 0.1f; bobSpeed = 10f;
                    break;
                case Kind.Soda:
                    frames = new[] { GameAssets.Pixel("soda") };
                    height = 0.62f; y = 0.8f;
                    hitSize = new Vector2(0.7f, 0.8f); hitOffset = Vector2.zero;
                    bob = 0.12f; bobSpeed = 4f;
                    break;
                case Kind.Shield:
                    frames = new[] { GameAssets.Smooth("bubble") };
                    height = 0.8f; y = 0.9f;
                    hitSize = new Vector2(0.85f, 0.85f); hitOffset = Vector2.zero;
                    bob = 0.12f; bobSpeed = 4f;
                    break;
                case Kind.Clock:
                    frames = new[] { GameAssets.Pixel("clock") };
                    height = 0.65f; y = 0.9f;
                    hitSize = new Vector2(0.8f, 0.8f); hitOffset = Vector2.zero;
                    bob = 0.12f; bobSpeed = 4f;
                    break;
                default: // Heart
                    frames = new[] { GameAssets.Pixel("heart") };
                    height = 0.6f; y = 0.9f;
                    hitSize = new Vector2(0.75f, 0.75f); hitOffset = Vector2.zero;
                    bob = 0.12f; bobSpeed = 4f;
                    break;
            }
            sr.sprite = frames[0];
            if (sr.sprite != null)
            {
                float s = height / sr.sprite.bounds.size.y;
                transform.localScale = new Vector3(s, s, 1f);
            }
            baseY = y;
            t = Random.value * 10f;
            transform.position = new Vector3(x, y, 0f);
        }

        /// <summary>Place a pickup at a specific height (used to put them in jump arcs).</summary>
        public void SetHeight(float y)
        {
            baseY = y;
            var p = transform.position;
            transform.position = new Vector3(p.x, y, 0f);
        }

        public void Tick(float dt, float worldSpeed)
        {
            t += dt;
            Vector3 p = transform.position;
            if (Smashed)
            {
                smashVelocity.y -= 30f * dt;
                p += (Vector3)(smashVelocity * dt);
                p.x -= worldSpeed * dt * 0.5f;
                transform.Rotate(0f, 0f, 720f * dt);
                transform.position = p;
                return;
            }

            p.x -= (worldSpeed + extraSpeed) * dt;
            p.y = baseY + (bob > 0f ? Mathf.Sin(t * bobSpeed) * bob : 0f);
            transform.position = p;

            if (frames.Length > 1 && fps > 0f) sr.sprite = frames[(int)(t * fps) % frames.Length];
            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        }

        public void Smash(float direction)
        {
            Smashed = true;
            smashVelocity = new Vector2(6f * direction, 11f);
            sr.sortingOrder = 30;
        }
    }
}
