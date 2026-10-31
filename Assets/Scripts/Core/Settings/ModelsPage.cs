using System;
using System.Text.RegularExpressions;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core.Settings
{
    public sealed class ModelsPage : ISettingsPage
    {
        public string Id => "models";
        public string Title => "Модели";
        public string IconId => "brain";
        public int Order => 20;
        public string[] Aliases => new[] { "модели", "модель", "llm", "умные", "нейросеть", "ollama", "openrouter", "groq", "gigachat", "гигачат", "yandexgpt", "яндексгпт" };

        private static readonly Regex UrlRegex = new Regex(
            @"^https?://[^\s/$.?#].[^\s]*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void Build(ISettingsView view)
        {
            view.Section("Умные ответы (LLM)");
            view.Hint("Если правила не поняли фразу, агент спросит языковую модель. Порядок: Ollama, российские облачные, зарубежные, модель в телефоне.");

            view.Toggle(
                "Включить умные ответы",
                () => LlmSettings.Enabled,
                v => SetEnabled(v));

            view.Section("Российские облачные");
            view.Hint("Работают без VPN. Нужен свой ключ: GigaChat - «ключ авторизации» из кабинета developers.sber.ru (нужны сертификаты Минцифры в сборке), YandexGPT - API-ключ и ID каталога из Yandex Cloud.");
            view.Input(
                "Ключ GigaChat", "ключ авторизации", true, () => string.Empty,
                v => SaveIfNotEmpty("gigachat", "GigaChat", v));
            view.Input(
                "Ключ YandexGPT", "API-ключ", true, () => string.Empty,
                v => SaveIfNotEmpty("yandex", "YandexGPT", v));
            view.Input(
                "ID каталога Yandex Cloud", "b1g...", false,
                () => ProjectOf("yandex"),
                v => SaveProject(v));

            view.Section("Зарубежные (необязательно)");
            view.Hint("Могут не работать без VPN.");
            view.Input(
                "Ключ OpenRouter", "sk-or-...", true, () => string.Empty,
                v => SaveIfNotEmpty("openrouter", "OpenRouter", v));
            view.Input(
                "Ключ Groq", "gsk_...", true, () => string.Empty,
                v => SaveIfNotEmpty("groq", "Groq", v));

            view.Section("Свой компьютер (Ollama)");
            view.Input(
                "Адрес Ollama",
                LlmSettings.DefaultUrl,
                false,
                () => LlmSettings.HasCustomUrl ? LlmSettings.Url : string.Empty,
                v => ValidateAndSaveOllamaUrl(v));

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

        /// <summary>«включи / выключи умные ответы», «включи / выключи модель в телефоне». Ключи голосом не принимаются.</summary>
        public bool TryVoice(string normalized, out string reply)
        {
            reply = null;
            if (string.IsNullOrEmpty(normalized)) return false;

            bool off = normalized.Contains("выключ") || normalized.Contains("отключ");
            bool on = normalized.Contains("включ");
            if (on == off) return false;

            if (normalized.Contains("умн") && normalized.Contains("ответ"))
            {
                reply = SetEnabled(on);
                return true;
            }
            if (normalized.Contains("модель") && normalized.Contains("телефон"))
            {
                reply = ToggleDeviceModel(on);
                return true;
            }
            return false;
        }

        private static string SetEnabled(bool v)
        {
            LlmSettings.Enabled = v;
            return v ? "Умные ответы включены" : "Умные ответы выключены";
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

        private static string SaveIfNotEmpty(string name, string title, string value)
        {
            string key = (value ?? "").Trim();
            return key.Length == 0 ? "Ключ " + title + " не изменён" : SaveCloudKey(name, key);
        }

        private static string SaveCloudKey(string name, string key)
        {
            LlmProfiles.Preset preset;
            if (!LlmProfiles.TryGetPreset(name, out preset)) return "Неизвестный провайдер: " + name;
            string model = preset.Model;
            string project = string.Empty;
            foreach (var p in LlmProfiles.Extra())
            {
                if (!string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(p.model)) model = p.model;
                project = p.project ?? string.Empty;
            }
            LlmProfiles.AddOrReplace(new LlmProfile { name = name, kind = preset.Kind, url = preset.Url, model = model, apiKey = key, project = project });
            if (name == "yandex" && project.Length == 0)
                return "Ключ yandex сохранён. Теперь введите ID каталога, без него YandexGPT не работает";
            return "Ключ " + name + " сохранён";
        }

        private static string ProjectOf(string name)
        {
            foreach (var p in LlmProfiles.Extra())
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p.project ?? string.Empty;
            return string.Empty;
        }

        private static string SaveProject(string value)
        {
            string id = (value ?? "").Trim();
            if (id.Length == 0) return "ID каталога не изменён";
            foreach (var p in LlmProfiles.Extra())
            {
                if (!string.Equals(p.name, "yandex", StringComparison.OrdinalIgnoreCase)) continue;
                p.project = id;
                LlmProfiles.AddOrReplace(p);
                return "ID каталога сохранён";
            }
            return "Сначала введите ключ YandexGPT";
        }
    }
}
