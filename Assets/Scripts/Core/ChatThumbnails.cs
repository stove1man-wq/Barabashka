using System.IO;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>
/// Маленькие картинки для карточек чата. Берёт готовую миниатюру (её делает Android-плагин при выборе файла),
/// а если её нет, уменьшает саму картинку, чтобы история чата не занимала много памяти.
/// </summary>
public static class ChatThumbnails
{
    /// <summary>Текстура миниатюры или null, если файла нет или он не читается. Вызывающий отвечает за её освобождение.</summary>
    public static Texture2D Load(MediaItem item, int maxSide)
    {
        if (item == null) return null;
        // Видео и прочие файлы без готовой миниатюры картинкой не читаем.
        if (string.IsNullOrEmpty(item.ThumbnailUri) && item.Kind != MediaKind.Image) return null;
        string path = MediaCodec.ToLocalPath(!string.IsNullOrEmpty(item.ThumbnailUri) ? item.ThumbnailUri : item.Uri);
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            return Mathf.Max(texture.width, texture.height) > maxSide ? Downscale(texture, maxSide) : texture;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Chat] Не удалось загрузить миниатюру: " + ex.Message);
            return null;
        }
    }

    private static Texture2D Downscale(Texture2D source, int maxSide)
    {
        float k = maxSide / (float)Mathf.Max(source.width, source.height);
        int w = Mathf.Max(1, Mathf.RoundToInt(source.width * k));
        int h = Mathf.Max(1, Mathf.RoundToInt(source.height * k));

        RenderTexture target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            small.wrapMode = TextureWrapMode.Clamp;
            return small;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(source);
        }
    }
}
