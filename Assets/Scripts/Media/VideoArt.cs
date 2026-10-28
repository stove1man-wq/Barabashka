using System.Collections.Generic;
using UnityEngine;

namespace Barabashka.Media
{
    /// <summary>
    /// Значки видеоплеера, рисуются кодом: "play", "pause", "prev", "nexttrack", "rew", "fwd", "speaker".
    /// Белые на прозрачном, цвет задаётся через Image.color.
    /// </summary>
    internal static class VideoArt
    {
        private const int Size = 96;
        private const int Samples = 3;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Icon(string id)
        {
            Sprite sprite;
            if (Cache.TryGetValue(id, out sprite) && sprite != null) return sprite;

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / Samples) / Size;
                            float v = (y + (sy + 0.5f) / Samples) / Size;
                            if (Inside(id, u, v)) hit++;
                        }
                    }
                    byte a = (byte)Mathf.RoundToInt(255f * hit / (Samples * Samples));
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = "Barabashka_Video_Icon_" + id;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            Cache[id] = sprite;
            return sprite;
        }

        private static bool Inside(string id, float x, float y)
        {
            switch (id)
            {
                case "pause":
                    return Box(x, y, 0.27f, 0.22f, 0.43f, 0.78f) || Box(x, y, 0.57f, 0.22f, 0.73f, 0.78f);
                case "prev":
                    return Box(x, y, 0.22f, 0.24f, 0.31f, 0.76f) || Tri(x, y, 0.78f, 0.24f, 0.78f, 0.76f, 0.34f, 0.50f);
                case "nexttrack":
                    return Box(x, y, 0.69f, 0.24f, 0.78f, 0.76f) || Tri(x, y, 0.22f, 0.24f, 0.22f, 0.76f, 0.66f, 0.50f);
                case "rew":
                    return Tri(x, y, 0.50f, 0.38f, 0.50f, 0.80f, 0.14f, 0.59f) || Tri(x, y, 0.86f, 0.38f, 0.86f, 0.80f, 0.50f, 0.59f);
                case "fwd":
                    return Tri(x, y, 0.14f, 0.38f, 0.14f, 0.80f, 0.50f, 0.59f) || Tri(x, y, 0.50f, 0.38f, 0.50f, 0.80f, 0.86f, 0.59f);
                case "speaker":
                {
                    if (Box(x, y, 0.12f, 0.38f, 0.32f, 0.62f)) return true;
                    if (Quad(x, y, 0.32f, 0.38f, 0.56f, 0.20f, 0.56f, 0.80f, 0.32f, 0.62f)) return true;
                    float dx = x - 0.56f, dy = y - 0.50f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    return dx > 0.04f && r > 0.20f && r < 0.27f && Mathf.Abs(dy) < dx * 1.4f;
                }
                default: // "play"
                    return Tri(x, y, 0.32f, 0.20f, 0.32f, 0.80f, 0.80f, 0.50f);
            }
        }

        private static bool Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        private static bool Quad(float x, float y, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            return Tri(x, y, ax, ay, bx, by, cx, cy) || Tri(x, y, ax, ay, cx, cy, dx, dy);
        }

        // Точка внутри треугольника при любом порядке вершин.
        private static bool Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
    }
}
