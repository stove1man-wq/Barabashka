using System;
using System.Collections.Generic;
using System.IO;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>
    /// Папка с документами и общие правила для инструментов документов.
    /// Путь совпадает с тем, куда выбор файлов кладёт вложения (см. Barabashka.Media.MediaPicker.InboxFolder):
    /// сборка Core не знает про Media, поэтому путь собран здесь.
    /// </summary>
    public static class DocsStore
    {
        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "media-inbox", "documents"); }
        }

        private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".txt", ".xls", ".xlsx"
        };

        /// <summary>
        /// Переносит выбранный файл (окно выбора уже скопировало его в каталог приложения, адрес file://) в папку документов.
        /// Возвращает имя сохранённого файла или null, если не получилось.
        /// </summary>
        public static string Import(MediaItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Uri) || !item.Uri.StartsWith("file://", StringComparison.Ordinal)) return null;
            try
            {
                string source = new Uri(item.Uri).LocalPath;
                string name = SafeFileName(string.IsNullOrEmpty(item.Title) ? Path.GetFileName(source) : item.Title);
                if (!Path.HasExtension(name)) name += Path.GetExtension(source);
                Directory.CreateDirectory(Folder);
                File.Copy(source, Path.Combine(Folder, name), true);
                try { File.Delete(source); } catch (Exception) { }   // копия окна выбора больше не нужна
                return name;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Docs] Не удалось сохранить документ: " + ex.Message);
                return null;
            }
        }

        public static string SafeFileName(string fileName)
        {
            var sb = new System.Text.StringBuilder();
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in fileName ?? string.Empty)
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            return sb.Length == 0 ? "document_" + Guid.NewGuid().ToString("N") : sb.ToString();
        }

        public static bool IsSupported(string extension)
        {
            return !string.IsNullOrEmpty(extension) && Supported.Contains(extension);
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " байт";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " КБ";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " МБ";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " ГБ";
        }

        public static string MimeType(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".txt": return "text/plain";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                default: return "application/octet-stream";
            }
        }
    }
}
