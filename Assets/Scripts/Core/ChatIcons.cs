using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Значки кнопок разделов чата. Рисуются кодом при первом обращении: белый значок на прозрачном фоне,
/// цвет задаётся через Image.color. Идентификаторы: all, dialog, phone, notes, telegram, system, purchases.
/// </summary>
public static class ChatIcons
{
    private const int Size = 96;

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    private delegate float Shape(float x, float y);

    // Многоугольники (плоские списки x, y); порядок вершин любой.
    private static readonly float[] BubbleTail = { 0.24f, 0.40f, 0.46f, 0.40f, 0.26f, 0.17f };
    private static readonly float[] PlaneWing1 = { 0.08f, 0.52f, 0.92f, 0.86f, 0.40f, 0.38f };
    private static readonly float[] PlaneWing2 = { 0.92f, 0.86f, 0.62f, 0.12f, 0.46f, 0.34f };
    private static readonly float[] PlayCut = { 0.40f, 0.29f, 0.40f, 0.71f, 0.73f, 0.50f };
    private static readonly float[] MediaPlay = { 0.43f, 0.37f, 0.43f, 0.63f, 0.64f, 0.50f };
    private static readonly float[] SpeakerCone = { 0.32f, 0.38f, 0.52f, 0.24f, 0.52f, 0.76f, 0.32f, 0.62f };
    private static readonly float[] ShieldTip = { 0.22f, 0.50f, 0.78f, 0.50f, 0.50f, 0.10f };
    private static readonly float[] CartBasket = { 0.30f, 0.40f, 0.84f, 0.40f, 0.92f, 0.68f, 0.26f, 0.68f };

    public static Sprite Get(string id)
    {
        Sprite sprite;
        if (Cache.TryGetValue(id, out sprite) && sprite != null) return sprite;
        sprite = Build(id);
        Cache[id] = sprite;
        return sprite;
    }

    private static Sprite Build(string id)
    {
        Shape shape = ShapeFor(id);
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float d = shape((x + 0.5f) / Size, (y + 0.5f) / Size);
                float a = Mathf.Clamp01(0.5f - d * Size);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.name = "Barabashka_Icon_" + id;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Расстояние до фигуры со знаком: меньше нуля - внутри. Объединение = Min, вычитание = Max(a, -b).
    private static Shape ShapeFor(string id)
    {
        switch (id)
        {
            case "chat":
            case "dialog":
                return (x, y) =>
                {
                    float d = Mathf.Min(RoundRect(x, y, 0.5f, 0.56f, 0.36f, 0.25f, 0.11f), Poly(x, y, BubbleTail));
                    d = Mathf.Max(d, -Circle(x, y, 0.34f, 0.56f, 0.045f));
                    d = Mathf.Max(d, -Circle(x, y, 0.50f, 0.56f, 0.045f));
                    return Mathf.Max(d, -Circle(x, y, 0.66f, 0.56f, 0.045f));
                };
            case "phone":
                return (x, y) =>
                {
                    float d = RoundRect(x, y, 0.5f, 0.5f, 0.19f, 0.34f, 0.07f);
                    d = Mathf.Max(d, -RoundRect(x, y, 0.5f, 0.55f, 0.135f, 0.235f, 0.02f));
                    return Mathf.Min(d, Circle(x, y, 0.5f, 0.245f, 0.03f));
                };
            case "notes":
                return (x, y) =>
                {
                    float d = RoundRect(x, y, 0.5f, 0.5f, 0.27f, 0.34f, 0.055f);
                    d = Mathf.Max(d, -RoundRect(x, y, 0.5f, 0.66f, 0.165f, 0.03f, 0.03f));
                    d = Mathf.Max(d, -RoundRect(x, y, 0.5f, 0.52f, 0.165f, 0.03f, 0.03f));
                    return Mathf.Max(d, -RoundRect(x, y, 0.415f, 0.38f, 0.08f, 0.03f, 0.03f));
                };
            case "telegram":
                return (x, y) => Mathf.Min(Poly(x, y, PlaneWing1), Poly(x, y, PlaneWing2));
            case "volume":
                // Динамик: корпус, раструб и две дуги звука справа.
                return (x, y) =>
                {
                    float d = Mathf.Min(RoundRect(x, y, 0.27f, 0.50f, 0.09f, 0.11f, 0.02f), Poly(x, y, SpeakerCone));
                    float wave1 = Mathf.Max(Mathf.Abs(Circle(x, y, 0.50f, 0.50f, 0.21f)) - 0.035f, Mathf.Max(0.60f - x, Mathf.Abs(y - 0.5f) - 0.20f));
                    float wave2 = Mathf.Max(Mathf.Abs(Circle(x, y, 0.50f, 0.50f, 0.34f)) - 0.035f, Mathf.Max(0.63f - x, Mathf.Abs(y - 0.5f) - 0.30f));
                    return Mathf.Min(d, Mathf.Min(wave1, wave2));
                };
            case "brain":
                // Лампочка («умные ответы»): колба, цоколь и основание с зазором.
                return (x, y) =>
                {
                    float d = Mathf.Min(Circle(x, y, 0.5f, 0.60f, 0.28f), RoundRect(x, y, 0.5f, 0.34f, 0.13f, 0.10f, 0.03f));
                    d = Mathf.Max(d, -RoundRect(x, y, 0.5f, 0.245f, 0.30f, 0.012f, 0f));
                    return Mathf.Min(d, RoundRect(x, y, 0.5f, 0.15f, 0.10f, 0.04f, 0.02f));
                };
            case "shield":
                // Щит с галочкой («действия агента»).
                return (x, y) =>
                {
                    float d = Mathf.Min(RoundRect(x, y, 0.5f, 0.62f, 0.28f, 0.24f, 0.04f), Poly(x, y, ShieldTip));
                    float tick = Mathf.Min(Segment(x, y, 0.37f, 0.52f, 0.46f, 0.41f, 0.035f), Segment(x, y, 0.46f, 0.41f, 0.64f, 0.65f, 0.035f));
                    return Mathf.Max(d, -tick);
                };
            case "settings":
            case "system":
                return (x, y) =>
                {
                    float d = Circle(x, y, 0.5f, 0.5f, 0.27f);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        d = Mathf.Min(d, RoundRect(x, y, 0.5f + 0.30f * Mathf.Cos(a), 0.5f + 0.30f * Mathf.Sin(a), 0.075f, 0.05f, 0.02f, a));
                    }
                    return Mathf.Max(d, -Circle(x, y, 0.5f, 0.5f, 0.115f));
                };
            case "play":
                return (x, y) => Mathf.Max(Circle(x, y, 0.5f, 0.5f, 0.46f), -Poly(x, y, PlayCut));
            case "fullscreen":
                return (x, y) =>
                {
                    // Четыре угловые скобки (значок полноэкранного режима): отражаем точку в левый нижний угол.
                    float fx = x < 0.5f ? x : 1f - x;
                    float fy = y < 0.5f ? y : 1f - y;
                    return Mathf.Min(BoxDist(fx, fy, 0.14f, 0.14f, 0.40f, 0.23f), BoxDist(fx, fy, 0.14f, 0.14f, 0.23f, 0.40f));
                };
            case "media":
                return (x, y) =>
                {
                    float ring = Mathf.Max(RoundRect(x, y, 0.5f, 0.5f, 0.38f, 0.29f, 0.09f), -RoundRect(x, y, 0.5f, 0.5f, 0.305f, 0.215f, 0.04f));
                    return Mathf.Min(ring, Poly(x, y, MediaPlay));
                };
            case "purchases":
                return (x, y) =>
                {
                    float d = Poly(x, y, CartBasket);
                    d = Mathf.Min(d, Segment(x, y, 0.08f, 0.82f, 0.20f, 0.82f, 0.03f));
                    d = Mathf.Min(d, Segment(x, y, 0.20f, 0.82f, 0.30f, 0.40f, 0.03f));
                    d = Mathf.Min(d, Circle(x, y, 0.42f, 0.22f, 0.065f));
                    return Mathf.Min(d, Circle(x, y, 0.76f, 0.22f, 0.065f));
                };
            default: // "all": четыре квадрата
                return (x, y) =>
                {
                    float d = RoundRect(x, y, 0.31f, 0.31f, 0.15f, 0.15f, 0.045f);
                    d = Mathf.Min(d, RoundRect(x, y, 0.69f, 0.31f, 0.15f, 0.15f, 0.045f));
                    d = Mathf.Min(d, RoundRect(x, y, 0.31f, 0.69f, 0.15f, 0.15f, 0.045f));
                    return Mathf.Min(d, RoundRect(x, y, 0.69f, 0.69f, 0.15f, 0.15f, 0.045f));
                };
        }
    }

    // Прямоугольник по краям; меньше нуля - внутри.
    private static float BoxDist(float x, float y, float x0, float y0, float x1, float y1)
    {
        return Mathf.Max(Mathf.Max(x0 - x, x - x1), Mathf.Max(y0 - y, y - y1));
    }

    private static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    private static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r, float angle = 0f)
    {
        float dx = x - cx, dy = y - cy;
        if (angle != 0f)
        {
            float c = Mathf.Cos(-angle), s = Mathf.Sin(-angle);
            float rx = dx * c - dy * s;
            float ry = dx * s + dy * c;
            dx = rx;
            dy = ry;
        }
        float qx = Mathf.Abs(dx) - hx + r, qy = Mathf.Abs(dy) - hy + r;
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float px = x - ax, py = y - ay;
        float ex = bx - ax, ey = by - ay;
        float h = Mathf.Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
        float dx = px - ex * h, dy = py - ey * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    // Выпуклый многоугольник: расстояние до самой дальней стороны (внутри < 0).
    private static float Poly(float x, float y, float[] p)
    {
        int n = p.Length / 2;
        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += p[2 * i] * p[2 * j + 1] - p[2 * j] * p[2 * i + 1];
        }
        float sign = area < 0f ? -1f : 1f;
        float d = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            float ax = p[2 * i], ay = p[2 * i + 1];
            float ex = p[2 * j] - ax, ey = p[2 * j + 1] - ay;
            float len = Mathf.Sqrt(ex * ex + ey * ey);
            float di = ((x - ax) * ey - (y - ay) * ex) / len * sign;
            d = Mathf.Max(d, di);
        }
        return d;
    }
}
