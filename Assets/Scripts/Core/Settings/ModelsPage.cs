using System;
using System.Text.RegularExpressions;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core.Settings
{
    public sealed class ModelsPage : ISettingsPage
    {
        public string Id => "models";
        public string Title => "Умные ответы";
        public string IconId => "brain";
        public int Order => 20;
        public string[] Aliases => new[] { "модели", "llm", "умные", "нейросеть", "ollama", "openrouter", "groq" };

        private static readonly Regex UrlRegex = new Regex(
            @"^https?://[^\s/$.?#].[^\s]*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void Build(ISettingsView view)
        {
            view.Section("Умные ответы (LLM)");
            view.Hint("Если правила не поняли фразу, агент спросит языковую модель. Приоритет: Ollama → OpenRouter → Groq → модель в телефоне.");

            view.Toggle(
                "Включить умные ответы",
                () => LlmSettings.Enabled,
                v => { LlmSettings.Enabled = v; return v ? "Умные ответы включены" : "Умные ответы выключены"; });

            view.Input(
                "Адрес Ollama",
                LlmSettings.DefaultUrl,
                false,
                () => LlmSettings.HasCustomUrl ? LlmSettings.Url : string.Empty,
                v => ValidateAndSaveOllamaUrl(v));

            view.Input(
                "Ключ OpenRouter", "sk-or-...", true, () => string.Empty,
                v => { string k = (v ?? "").Trim(); return k.Length == 0 ? "Ключ OpenRouter не изменён" : SaveCloudKey("openrouter", k); });

            view.Input(
                "Ключ Groq", "gsk_...", true, () => string.Empty,
                v => { string k = (v ?? "").Trim(); return k.Length == 0 ? "Ключ Groq не изменён" : SaveCloudKey("groq", k); });

            BuildDeviceModelSection(view);
        }

        private void BuildDeviceModelSection(ISettingsView view)
        {
            view.Section("Модель в телефоне (офлайн)");
            view.Hint("Qwen2.5 1.5B (~1 ГБ). Работает без интернета, но медленнее облачных моделей.");
            view.Toggle(
                "Использовать модель в телефоне",
                () => HasDeviceProfile(),
                v => ToggleDeviceModel(v));
        }

        public bool TryVoice(string normalized, out string reply)
        {
            reply = null; return false;
        }

        private static string ValidateAndSaveOllamaUrl(string v)
        {
            string t = (v ?? "").Trim();
            if (t.Length == 0) { LlmSettings.ClearUrl(); return "Адрес Ollama сброшен к умолчанию (" + LlmSettings.DefaultUrl + ")"; }
            if (!UrlRegex.IsMatch(t) || !(t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return "Адрес Ollama не похож на URL. Пример: http://192.168.0.5:11434";
            LlmSettings.Url = t; return "Адрес Ollama сохранён: " + t;
        }

        private static bool HasDeviceProfile()
        {
            foreach (var p in LlmProfiles.Extra()) if (p.IsDevice) return true; return false;
        }

        private static string ToggleDeviceModel(bool v)
        {
            string note = null;
            if (v)
            {
                if (LocalModel.Exists(LocalModel.DefaultPath))
                    LlmProfiles.AddOrReplace(new LlmProfile { name = LocalModel.ProfileName, kind = "device", url = "", model = LocalModel.DefaultPath, apiKey = "" });
                else if (LocalModel.StartDownload(null)) note = "Скачиваю модель для работы без сети (около 1 ГБ), скажу, когда закончу.";
            }
            else if (HasDeviceProfile())
            {
                LlmProfiles.Remove(LocalModel.ProfileName);
                if (Barabashka.Contracts.LocalLlmHost.Current != null) Barabashka.Contracts.LocalLlmHost.Current.Unload();
            }
            return note ?? (v ? "Модель в телефоне включена" : "Модель в телефоне выключена");
        }

        private static string SaveCloudKey(string name, string key)
        {
            LlmProfiles.Preset preset;
            if (!LlmProfiles.TryGetPreset(name, out preset)) return "Неизвестный провайдер: " + name;
            string model = preset.Model;
            foreach (var p in LlmProfiles.Extra())
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.model)) model = p.model;
            LlmProfiles.AddOrReplace(new LlmProfile { name = name, kind = preset.Kind, url = preset.Url, model = model, apiKey = key });
            return "Ключ " + name + " сохранён";
        }
    }
}