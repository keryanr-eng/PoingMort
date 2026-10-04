using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.UI
{
    /// <summary>Procedural UI sprites (rounded panels, circles, rings) so the UI needs no imported textures.</summary>
    public static class UISprites
    {
        static readonly Dictionary<string, Sprite> s_Cache = new Dictionary<string, Sprite>();

        /// <summary>9-sliced rounded rectangle, white, to be tinted by the Image colour.</summary>
        public static Sprite RoundedRect(int radius = 10)
        {
            string key = "rr" + radius;
            if (s_Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = radius * 2 + 4;
            var tex = NewTexture(size, size, key);
            var pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            sprite.name = key;
            s_Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Circle(int size = 64) => Ring(size, size * 0.5f);

        /// <summary>Anti-aliased ring (thickness in pixels). Use with Image.Type.Filled for gauges.</summary>
        public static Sprite Ring(int size = 128, float thickness = 12f)
        {
            string key = "ring" + size + "_" + thickness;
            if (s_Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = NewTexture(size, size, key);
            var pixels = new Color32[size * size];
            float outer = size * 0.5f - 1f;
            float inner = Mathf.Max(0f, outer - thickness);
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            s_Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Vertical gradient used for subtle shading of large panels.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            string key = "grad" + top + bottom;
            if (s_Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int h = 64;
            var tex = NewTexture(2, h, key);
            var pixels = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                Color col = Color.Lerp(bottom, top, y / (h - 1f));
                pixels[y * 2] = col;
                pixels[y * 2 + 1] = col;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            s_Cache[key] = sprite;
            return sprite;
        }

        static Texture2D NewTexture(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            return tex;
        }
    }
}
