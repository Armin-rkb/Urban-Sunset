using System.Collections.Generic;
using UnityEngine;

namespace UrbanSunset
{
    /// <summary>
    /// Fits the camera to any screen shape and scrolls the parallax layers
    /// (sunset sky, city silhouette, street lamps and the street itself).
    /// </summary>
    public class WorldScroller : MonoBehaviour
    {
        public static readonly Color SkyTop = new Color32(159, 40, 0, 255);
        static readonly Color GroundFill = new Color32(20, 48, 76, 255);

        const float GroundVisible = 1.7f;   // world units of street visible under the skater's feet
        const float MinViewWidth = 13f;     // keep enough look-ahead on portrait phones
        const float LandscapeSize = 5f;

        class Layer
        {
            public SpriteRenderer sr;
            public float factor;
            public float tileWidth; // world units
            public float offset;
        }

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Transform> lamps = new List<Transform>();
        Camera cam;
        Layer sky, city, ground;
        SpriteRenderer groundFill;
        float nextLamp = 4f;
        int lastW, lastH;
        Vector3 basePos;

        public float ViewWidth { get; private set; }
        public float ViewHeight { get; private set; }
        public float Left { get { return basePos.x - ViewWidth * 0.5f; } }
        public float Right { get { return basePos.x + ViewWidth * 0.5f; } }
        public float Top { get { return basePos.y + ViewHeight * 0.5f; } }

        public void Init(Camera camera)
        {
            cam = camera;
            cam.orthographic = true;
            cam.backgroundColor = SkyTop;
            cam.clearFlags = CameraClearFlags.SolidColor;

            sky = AddLayer("Sky", GameAssets.Smooth("sky"), 0.06f, -30, Color.white);
            city = AddLayer("City", GameAssets.Pixel("buildings"), 0.3f, -20, new Color(0.42f, 0.22f, 0.28f, 1f));
            ground = AddLayer("Street", GameAssets.Pixel("ground"), 1f, 5, Color.white);

            var fill = new GameObject("StreetFill");
            fill.transform.SetParent(transform, false);
            groundFill = fill.AddComponent<SpriteRenderer>();
            groundFill.sprite = GameAssets.White;
            groundFill.color = GroundFill;
            groundFill.sortingOrder = 4;

            Layout();
        }

        Layer AddLayer(string name, Sprite sprite, float factor, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            sr.color = tint;
            var layer = new Layer { sr = sr, factor = factor };
            layers.Add(layer);
            return layer;
        }

        /// <summary>Recomputes camera size and layer sizes. Safe to call every frame.</summary>
        public void Layout()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width;
            lastH = Screen.height;

            float aspect = Mathf.Max(0.1f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float size = Mathf.Max(LandscapeSize, MinViewWidth / (2f * aspect));
            cam.orthographicSize = size;
            ViewHeight = size * 2f;
            ViewWidth = ViewHeight * aspect;

            // Skater stands at x = 0, a quarter of the way into the screen (capped on ultra-wide screens).
            float playerFromLeft = Mathf.Min(ViewWidth * 0.25f, 5f);
            basePos = new Vector3(ViewWidth * 0.5f - playerFromLeft, size - GroundVisible, -10f);
            cam.transform.position = basePos;

            float top = Top;
            FitLayer(sky, Mathf.Min((top + 0.3f) / Height(sky), 2.8f), -0.3f);
            FitLayer(city, 1.45f, -0.05f);
            FitLayer(ground, GroundVisible / Height(ground), -GroundVisible);

            // Solid colour under the street in case the screen is very tall.
            groundFill.transform.localScale = new Vector3(ViewWidth * 2f, 40f, 1f);
            groundFill.transform.position = new Vector3(basePos.x, -GroundVisible - 20f + 0.01f, 0f);
        }

        static float Height(Layer l) { return l.sr.sprite != null ? l.sr.sprite.bounds.size.y : 1f; }

        void FitLayer(Layer l, float scale, float bottomY)
        {
            if (l.sr.sprite == null) return;
            Vector2 spriteSize = l.sr.sprite.bounds.size;
            l.sr.transform.localScale = new Vector3(scale, scale, 1f);
            l.tileWidth = spriteSize.x * scale;
            float worldWidth = ViewWidth + l.tileWidth * 2f;
            l.sr.size = new Vector2(worldWidth / scale, spriteSize.y);
            l.sr.transform.position = new Vector3(basePos.x, bottomY + spriteSize.y * scale * 0.5f, 0f);
        }

        /// <summary>Scrolls everything by the distance the world moved this frame.</summary>
        public void Scroll(float distance)
        {
            foreach (var l in layers)
            {
                l.offset = Mathf.Repeat(l.offset + distance * l.factor, Mathf.Max(0.01f, l.tileWidth));
                var p = l.sr.transform.position;
                l.sr.transform.position = new Vector3(basePos.x - l.offset, p.y, p.z);
            }

            // Street lamps on a mid layer for extra depth.
            const float lampFactor = 0.75f;
            nextLamp -= distance * lampFactor;
            if (nextLamp <= 0f)
            {
                SpawnLamp(Right + 1f);
                nextLamp = Random.Range(5f, 9f);
            }
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                var t = lamps[i];
                t.position += Vector3.left * distance * lampFactor;
                if (t.position.x < Left - 2f)
                {
                    Destroy(t.gameObject);
                    lamps.RemoveAt(i);
                }
            }
        }

        void SpawnLamp(float x)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.Pixel("lamp");
            sr.sortingOrder = -10;
            sr.color = new Color(0.75f, 0.6f, 0.65f, 1f);
            if (sr.sprite != null)
            {
                float s = 3f / sr.sprite.bounds.size.y;
                go.transform.localScale = new Vector3(s, s, 1f);
                go.transform.position = new Vector3(x, 1.5f - 0.05f, 0f);
            }
            lamps.Add(go.transform);
        }

        /// <summary>Applies a screen-shake offset on top of the fitted camera position.</summary>
        public void SetShake(Vector2 offset)
        {
            cam.transform.position = basePos + (Vector3)offset;
        }

        public void ClearLamps()
        {
            foreach (var t in lamps) Destroy(t.gameObject);
            lamps.Clear();
            nextLamp = 2f;
        }

        public void ForceLayout()
        {
            lastW = lastH = -1;
            Layout();
        }
    }
}
