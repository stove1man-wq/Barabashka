using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

namespace Barabashka.Core
{
    /// <summary>
    /// Инструмент для списка документов.
    /// Фразы: «мои документы», «покажи документы», «найди документ X»
    /// </summary>
    public sealed class DocsListTool : IAgentTool
    {
        public string Name => "docs_list";
        public string Description => "показать документы или найти среди них. {\"tool\":\"docs_list\"} или {\"tool\":\"docs_list\",\"query\":\"слово\"}";

        public Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
        {
            try
            {
                string query = ToolArgs.Get(args, "query").ToLowerInvariant();
                string documentsFolder = Path.Combine(MediaPicker.InboxFolder, "documents");
                
                if (!Directory.Exists(documentsFolder))
                {
                    return Task.FromResult(ToolResult.Ok("Документов пока нет."));
                }

                var documents = new List<FileInfo>();
                var dirInfo = new DirectoryInfo(documentsFolder);
                foreach (var file in dirInfo.GetFiles())
                {
                    if (IsSupportedDocument(file.Extension))
                    {
                        documents.Add(file);
                    }
                }

                if (documents.Count == 0)
                {
                    return Task.FromResult(ToolResult.Ok("Документов пока нет."));
                }

                var sb = new System.Text.StringBuilder();
                int shown = 0;
                
                foreach (var file in documents.OrderBy(f => f.Name))
                {
                    if (query.Length > 0 && !file.Name.ToLowerInvariant().Contains(query))
                        continue;

                    string size = FormatFileSize(file.Length);
                    sb.Append(shown + 1).Append(". ").Append(file.Name).Append(" (").Append(size).Append(")\n");
                    shown++;
                }

                if (shown == 0)
                {
                    return Task.FromResult(ToolResult.Ok($"В документах нет ничего про «{query}»."));
                }

                string header = query.Length > 0 ? "Нашёл в документах:\n" : "Ваши документы:\n";
                return Task.FromResult(ToolResult.Ok(header + sb.ToString().TrimEnd()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail("Ошибка при поиске документов: " + ex.Message));
            }
        }

        private bool IsSupportedDocument(string extension)
        {
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".pdf", ".doc", ".docx", ".txt", ".xls", ".xlsx"
            };
            return supportedExtensions.Contains(extension);
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " байт";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " КБ";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " МБ";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F1") + " ГБ";
        }
    }
}