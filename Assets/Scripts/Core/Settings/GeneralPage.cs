using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core.Settings
{
    public sealed class GeneralPage : ISettingsPage
    {
        public string Id => "general";
        public string Title => "Общие";
        public string IconId => "settings";
        public int Order => 50;
        public string[] Aliases => new[] { "общие", "город", "погода", "general" };

        public void Build(ISettingsView view)
        {
            view.Section("Город по умолчанию");
            view.Hint("Используется для погоды, если в запросе не указан город. Пусто — определять автоматически.");
            view.Input(
                "Город",
                "Казань",
                false,
                () => AgentWeather.DefaultCity()?.Name ?? string.Empty,
                v => ApplyCity(v));
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null; return false;
        }

        private static string ApplyCity(string city)
        {
            string trimmed = (city ?? string.Empty).Trim();
            AgentWeather.City current = AgentWeather.DefaultCity();
            if (trimmed.Length > 0 && (current == null || !string.Equals(current.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                _ = ApplyCityAsync(trimmed);
                return "Город будет обновлён: " + trimmed;
            }
            return "Город не изменён";
        }

        private static async Task ApplyCityAsync(string city)
        {
            IAgentTool tool;
            if (!AgentToolRegistry.Default.TryGet("set_city", out tool)) return;
            try
            {
                var args = new Dictionary<string, string> { { "city", city } };
                ToolResult result = await tool.ExecuteAsync(args, CancellationToken.None);
                Debug.Log("[Settings] Город: " + (result.Success ? result.Output : result.Error));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Settings] Город: " + ex.Message);
            }
        }
    }
}