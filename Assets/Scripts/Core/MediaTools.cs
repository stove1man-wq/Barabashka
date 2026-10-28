using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

/// <summary>Выбор картинки или видео через системное окно; результат появляется в чате миниатюрой (нажатие открывает просмотр).</summary>
public sealed class MediaPickTool : IAgentTool
{
    public string Name { get { return "media_pick"; } }

    public string Description
    {
        get
        {
            return "выбрать фото, картинку или видео на телефоне и показать в чате. {\"tool\":\"media_pick\",\"kind\":\"image|video\"}";
        }
    }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string kindName = Value(args, "kind");
        bool video = string.Equals(kindName, "video", StringComparison.OrdinalIgnoreCase);
        if (kindName.Length > 0 && !video && !string.Equals(kindName, "image", StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("Умею показывать картинки и видео.");

        IMediaPicker picker = MediaServices.Picker;
        if (picker == null)
            return ToolResult.Fail("Выбор файлов сейчас недоступен.");

        IReadOnlyList<MediaItem> items = await picker.PickAsync(video ? MediaKind.Video : MediaKind.Image, false, ct);
        if (items == null || items.Count == 0)
        {
            if (Application.platform == RuntimePlatform.Android) return ToolResult.Ok("Ничего не выбрано.");
            return ToolResult.Ok(video
                ? "Ничего не выбрано. Выбор видео на компьютере пока не поддерживается."
                : "Ничего не выбрано. На компьютере положите картинку в папку: " + Application.persistentDataPath + "/media-inbox");
        }

        MediaItem item = items[0];
        MediaServices.PendingAttachment = item;
        string fallback = item.Kind == MediaKind.Video ? "видео" : "картинка";
        return ToolResult.Ok("Выбрано: " + (string.IsNullOrEmpty(item.Title) ? fallback : item.Title) + ". Нажмите на миниатюру, чтобы открыть.");
    }

    private static string Value(IDictionary<string, string> args, string key)
    {
        string value;
        return args != null && args.TryGetValue(key, out value) && value != null ? value.Trim() : string.Empty;
    }
}

/// <summary>Управление открытым просмотром: дальше, назад, закрыть; для видео ещё пауза, продолжить, перемотка, скорость.</summary>
public sealed class MediaControlTool : IAgentTool
{
    public string Name { get { return "media_control"; } }

    public string Description
    {
        get
        {
            return "управлять открытым просмотром. {\"tool\":\"media_control\",\"action\":\"next|prev|close|pause|play|seek|faster|slower\"}; " +
                   "для seek ещё \"seconds\" (минус - назад)";
        }
    }

    private static ToolResult Playback(IMediaPlayback playback, string action, IDictionary<string, string> args)
    {
        if (playback == null || !playback.HasVideo) return ToolResult.Fail("Сейчас не открыто видео.");
        switch (action)
        {
            case "pause":
                playback.Pause();
                return ToolResult.Ok("Пауза.");
            case "play":
                playback.Play();
                return ToolResult.Ok("Продолжаю.");
            case "seek":
            {
                string text;
                double seconds;
                if (args == null || !args.TryGetValue("seconds", out text) ||
                    !double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds) || seconds == 0)
                    return ToolResult.Fail("Не понял, на сколько секунд перемотать.");
                playback.SeekBy(seconds);
                return ToolResult.Ok(seconds > 0 ? "Вперёд на " + (int)seconds + " сек." : "Назад на " + (int)(-seconds) + " сек.");
            }
            case "faster":
                playback.SetSpeed(1.5f);
                return ToolResult.Ok("Скорость 1,5x.");
            default: // slower
                playback.SetSpeed(0.5f);
                return ToolResult.Ok("Скорость 0,5x.");
        }
    }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        IMediaViewer viewer = MediaServices.Viewer;
        if (viewer == null || !viewer.IsOpen)
            return Task.FromResult(ToolResult.Fail("Сейчас ничего не открыто."));

        string action;
        if (args == null || !args.TryGetValue("action", out action)) action = string.Empty;
        switch ((action ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "next":
                return Task.FromResult(viewer.Next() ? ToolResult.Ok("Дальше.") : ToolResult.Ok("Это последняя."));
            case "prev":
                return Task.FromResult(viewer.Previous() ? ToolResult.Ok("Назад.") : ToolResult.Ok("Это первая."));
            case "close":
                viewer.Close();
                return Task.FromResult(ToolResult.Ok("Закрыто."));
            case "pause":
            case "play":
            case "seek":
            case "faster":
            case "slower":
                return Task.FromResult(Playback(viewer as IMediaPlayback, action.Trim().ToLowerInvariant(), args));
            default:
                return Task.FromResult(ToolResult.Fail("Не понял, что сделать с просмотром."));
        }
    }
}
