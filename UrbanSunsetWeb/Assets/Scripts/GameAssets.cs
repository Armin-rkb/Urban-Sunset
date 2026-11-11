using System.Collections.Generic;
using UnityEngine;

namespace UrbanSunset
{
    /// <summary>
    /// Loads every sprite, sound and font from Resources and builds a few procedural UI shapes.
    /// Nothing in the game is wired up in the Inspector, so the project builds from a blank scene.
    /// </summary>
    public static class GameAssets
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static Font font;
        static Sprite white, circle, ring, arrow, speakerOn, speakerOff;

        public static Sprite Pixel(string name) { return Load("Pixel/" + name); }
        public static Sprite Smooth(string name) { return Load("Smooth/" + name); }

        public static Sprite[] Frames(string prefix, int count)
        {
            var result = new Sprite[count];
            for (int i = 0; i < count; i++) result[i] = Pixel(prefix + "_" + i);
            return result;
        }

        static Sprite Load(string path)
        {
            Sprite s;
            if (!sprites.TryGetValue(path, out s))
            {
                s = Resources.Load<Sprite>(path);
                if (s == null) Debug.LogWarning("Missing sprite: " + path);
                sprites[path] = s;
            }
            return s;
        }

        public static AudioClip Clip(string name)
        {
            AudioClip c;
            if (!clips.TryGetValue(name, out c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                if (c == null) Debug.LogWarning("Missing audio: " + name);
                clips[name] = c;
            }
            return c;
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/PressStart2P");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Sprite White
        {
            get
            {
                if (white == null) white = MakeSprite(4, (x, y) => 1f, FilterMode.Point);
                return white;
            }
        }

        /// <summary>Anti-aliased filled disc.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int n = 128;
                    circle = MakeSprite(n, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                        return Mathf.Clamp01(n / 2f - d);
                    }, FilterMode.Bilinear);
                }
                return circle;
            }
        }

        /// <summary>Anti-aliased ring, used as the outline of the touch buttons.</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    const int n = 128;
                    ring = MakeSprite(n, (x, y) =>
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                        float outer = Mathf.Clamp01(n / 2f - d);
                        float inner = Mathf.Clamp01(d - (n / 2f - 6f));
                        return Mathf.Min(outer, inner);
                    }, FilterMode.Bilinear);
                }
                return ring;
            }
        }

        /// <summary>Upward-pointing chevron arrow. Rotate 180 degrees for a down arrow.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (arrow == null)
                {
                    const int n = 96;
                    arrow = MakeSprite(n, (x, y) =>
                    {
                        // Distance to a "^" shape made of two thick strokes.
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        Vector2 top = new Vector2(n * 0.5f, n * 0.78f);
                        float a = SegDist(p, new Vector2(n * 0.16f, n * 0.34f), top);
                        float b = SegDist(p, new Vector2(n * 0.84f, n * 0.34f), top);
                        return Mathf.Clamp01(n * 0.085f - Mathf.Min(a, b) + 0.5f);
                    }, FilterMode.Bilinear);
                }
                return arrow;
            }
        }

        /// <summary>Speaker icon, with sound waves (on) or a cross (off).</summary>
        public static Sprite Speaker(bool on)
        {
            if (on && speakerOn != null) return speakerOn;
            if (!on && speakerOff != null) return speakerOff;
            const int n = 96;
            var s = MakeSprite(n, (x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float a = 0f;
                // body
                if (u > 0.14f && u < 0.32f && v > 0.38f && v < 0.62f) a = 1f;
                // cone
                if (u >= 0.3f && u < 0.52f)
                {
                    float k = (u - 0.3f) / 0.22f;
                    float half = Mathf.Lerp(0.12f, 0.3f, k);
                    if (Mathf.Abs(v - 0.5f) < half) a = 1f;
                }
                Vector2 p = new Vector2(u, v);
                if (on)
                {
                    float d = Vector2.Distance(p, new Vector2(0.5f, 0.5f));
                    bool right = u > 0.58f;
                    if (right && Mathf.Abs(d - 0.2f) < 0.035f) a = 1f;
                    if (right && Mathf.Abs(d - 0.34f) < 0.035f && Mathf.Abs(v - 0.5f) < 0.26f) a = 1f;
                }
                else
                {
                    float d1 = SegDist(p * n, new Vector2(0.62f, 0.36f) * n, new Vector2(0.88f, 0.64f) * n) / n;
                    float d2 = SegDist(p * n, new Vector2(0.62f, 0.64f) * n, new Vector2(0.88f, 0.36f) * n) / n;
                    if (Mathf.Min(d1, d2) < 0.04f) a = 1f;
                }
                return a;
            }, FilterMode.Bilinear);
            if (on) speakerOn = s; else speakerOff = s;
            return s;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static Sprite MakeSprite(int size, System.Func<int, int, float> alpha, FilterMode filter)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = filter;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x, y) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
