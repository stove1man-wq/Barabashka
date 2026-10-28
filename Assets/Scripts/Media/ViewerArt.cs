using System.Collections.Generic;
using UnityEngine;

namespace Barabashka.Media
{
    /// <summary>
    /// Графика просмотрщика, рисуется кодом: скруглённая подложка кнопок (растягивается по краям) и белые значки
    /// (назад, вперёд, закрыть). Цвет значка задаётся через Image.color.
    /// </summary>
    internal static class ViewerArt
    {
        private const int IconSize = 96;
        private const int RoundSize = 64;

        private static Sprite _rounded;
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>Скруглённый прямоугольник; использовать с Image.type = Sliced.</summary>
        public static Sprite Rounded()
        {
            if (_rounded != null) return _rounded;

            const float radius = 20f;
            var pixels = new Color32[RoundSize * RoundSize];
            for (int y = 0; y < RoundSize; y++)
            {
                for (int x = 0; x < RoundSize; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - RoundSize * 0.5f) - (RoundSize * 0.5f - radius);
                    float dy = Mathf.Abs(y + 0.5f - RoundSize * 0.5f) - (RoundSize * 0.5f - radius);
                    float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * RoundSize + x] = new Color32(255, 255, 255, a);
                }
            }

            var texture = new Texture2D(RoundSize, RoundSize, TextureFormat.RGBA32, false);
            texture.name = "Barabashka_Viewer_Round";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            _rounded = Sprite.Create(texture, new Rect(0f, 0f, RoundSize, RoundSize), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(22f, 22f, 22f, 22f));
            return _rounded;
        }

        /// <summary>Значок: "back", "next", "close".</summary>
        public static Sprite Icon(string id)
        {
            Sprite sprite;
            if (Icons.TryGetValue(id, out sprite) && sprite != null) return sprite;

            var pixels = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float d = Distance(id, (x + 0.5f) / IconSize, (y + 0.5f) / IconSize);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d * IconSize) * 255f);
                    pixels[y * IconSize + x] = new Color32(255, 255, 255, a);
                }
            }

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.name = "Barabashka_Viewer_Icon_" + id;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
            Icons[id] = sprite;
            return sprite;
        }

        private static float Distance(string id, float x, float y)
        {
            const float t = 0.055f;
            switch (id)
            {
                case "next":
                    return Mathf.Min(Segment(x, y, 0.38f, 0.20f, 0.62f, 0.50f, t), Segment(x, y, 0.62f, 0.50f, 0.38f, 0.80f, t));
                case "close":
                    return Mathf.Min(Segment(x, y, 0.28f, 0.28f, 0.72f, 0.72f, t), Segment(x, y, 0.28f, 0.72f, 0.72f, 0.28f, t));
                default: // "back"
                    return Mathf.Min(Segment(x, y, 0.62f, 0.20f, 0.38f, 0.50f, t), Segment(x, y, 0.38f, 0.50f, 0.62f, 0.80f, t));
            }
        }

        // Расстояние до отрезка с круглыми концами (внутри меньше нуля).
        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay;
            float ex = bx - ax, ey = by - ay;
            float h = Mathf.Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
            float dx = px - ex * h, dy = py - ey * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }
    }
}
