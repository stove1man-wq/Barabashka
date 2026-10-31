using System;
using System.Collections.Generic;
using System.IO;
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
