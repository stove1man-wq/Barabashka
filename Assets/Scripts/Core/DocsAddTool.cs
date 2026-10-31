using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using Barabashka.Core;

/// <summary>Добавить документ: системное окно выбора (папка «Документы»), файл копируется в папку документов агента.</summary>
public sealed class DocsAddTool : IAgentTool
{
    public string Name { get { return "docs_add"; } }

    public string Description
    {
        get { return "добавить документ с телефона в папку документов через окно выбора файлов. {\"tool\":\"docs_add\"}"; }
    }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        IMediaPicker picker = MediaServices.Picker;
        if (picker == null) return ToolResult.Fail("Выбор файлов сейчас недоступен.");

        IReadOnlyList<MediaItem> items = await picker.PickAsync(MediaKind.Document, true, ct);
        if (items == null || items.Count == 0) return ToolResult.Ok("Ничего не выбрано.");

        var names = new List<string>();
        foreach (MediaItem item in items)
        {
            string saved = DocsStore.Import(item);
            if (saved != null) names.Add(saved);
        }
        if (names.Count == 0) return ToolResult.Fail("Не удалось сохранить выбранный файл. Попробуйте выбрать его из папки «Документы» или «Загрузки».");
        return ToolResult.Ok("Добавлено: " + string.Join(", ", names.ToArray()) + ". Скажите «открой документ ...» или «мои документы».");
    }
}
