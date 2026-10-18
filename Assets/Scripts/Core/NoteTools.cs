using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

public sealed class NoteAddTool : IAgentTool
{
    public string Name { get { return "note_add"; } }
    public string Description { get { return "записать заметку («запомни, что…»). {\"tool\":\"note_add\",\"text\":\"что запомнить\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string text = ToolArgs.Get(args, "text");
        if (text.Length == 0) return Task.FromResult(ToolResult.Fail("Что записать? Скажите, например: «запомни, что пароль от wifi лежит в ящике»."));
        if (text.Length > 1000) text = text.Substring(0, 1000);

        string error;
        AgentNotes.Note note = AgentNotes.Add(text, out error);
        if (note == null) return Task.FromResult(ToolResult.Fail(error));
        return Task.FromResult(ToolResult.Ok("Записал: «" + text + "»."));
    }
}

public sealed class NoteListTool : IAgentTool
{
    public string Name { get { return "note_list"; } }
    public string Description { get { return "показать заметки или найти среди них. {\"tool\":\"note_list\"} или {\"tool\":\"note_list\",\"query\":\"слово\"}"; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        List<AgentNotes.Note> notes = AgentNotes.All();
        if (notes.Count == 0) return Task.FromResult(ToolResult.Ok("Заметок пока нет."));

        string query = ToolArgs.Get(args, "query").ToLowerInvariant();
        var sb = new StringBuilder();
        int shown = 0;
        for (int i = 0; i < notes.Count; i++)
        {
            if (query.Length > 0 && notes[i].text.ToLowerInvariant().IndexOf(query, StringComparison.Ordinal) < 0) continue;
            sb.Append(i + 1).Append(". ").Append(notes[i].text).Append('\n');
            shown++;
        }

        if (shown == 0) return Task.FromResult(ToolResult.Ok("В заметках нет ничего про «" + query + "»."));
        string header = query.Length > 0 ? "Нашёл в заметках:\n" : "Ваши заметки:\n";
        return Task.FromResult(ToolResult.Ok(header + sb.ToString().TrimEnd()));
    }
}

public sealed class NoteDeleteTool : IAgentTool
{
    public string Name { get { return "note_delete"; } }
    public string Description { get { return "удалить заметки по номерам из списка. {\"tool\":\"note_delete\",\"numbers\":\"1,2\"}, все: \"ALL\""; } }

    public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string numbers = ToolArgs.Get(args, "numbers");
        if (numbers.Length == 0)
            return Task.FromResult(ToolResult.Fail("Какую заметку удалить? Назовите номер или попросите показать заметки."));

        int removed = AgentNotes.Delete(numbers);
        if (removed < 0) return Task.FromResult(ToolResult.Fail("Заметки с таким номером нет. Попросите показать заметки."));
        if (removed == 0) return Task.FromResult(ToolResult.Ok("Заметок нет."));
        return Task.FromResult(ToolResult.Ok(removed == 1 ? "Заметка удалена." : "Удалено заметок: " + removed + "."));
    }
}
