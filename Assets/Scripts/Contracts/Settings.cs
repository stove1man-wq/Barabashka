using System;
using System.Collections.Generic;

namespace Barabashka.Contracts
{
    /// <summary>
    /// View для построения страницы настроек. Реализация (UI) лежит в другой сборке и не знает про логику.
    /// Все set-функции применяют значение сразу и возвращают текст подтверждения или ошибки.
    /// </summary>
    public interface ISettingsView
    {
        /// <summary>Начать новую секцию с заголовком.</summary>
        void Section(string title);

        /// <summary>Подсказка/описание под секцией.</summary>
        void Hint(string text);

        /// <summary>Тумблер: получить текущее значение и установить новое.</summary>
        /// <param name="title">Заголовок тумблера.</param>
        /// <param name="get">Функция чтения текущего значения.</param>
        /// <param name="set">Функция записи: применяет значение и возвращает подтверждение или ошибку.</param>
        void Toggle(string title, Func<bool> get, Func<bool, string> set);

        /// <summary>Выбор из списка вариантов.</summary>
        /// <param name="title">Заголовок.</param>
        /// <param name="options">Массив текстов вариантов.</param>
        /// <param name="get">Текущий индекс.</param>
        /// <param name="set">Запись индекса; возвращает подтверждение или ошибку.</param>
        void Choice(string title, string[] options, Func<int> get, Func<int, string> set);

        /// <summary>Текстовое поле ввода.</summary>
        /// <param name="caption">Подпись поля.</param>
        /// <param name="placeholder">Подсказка внутри пустого поля.</param>
        /// <param name="secret">true — пароль/ключ (маскируется в UI).</param>
        /// <param name="get">Текущее значение.</param>
        /// <param name="set">Запись значения; возвращает подтверждение или ошибку. Пустая строка — не менять сохранённое.</param>
        void Input(string caption, string placeholder, bool secret, Func<string> get, Func<string, string> set);
    }

    /// <summary>
    /// Страница настроек. Построение через ISettingsView; голосовое управление через TryVoice.
    /// </summary>
    public interface ISettingsPage
    {
        /// <summary>Уникальный идентификатор страницы (stable).</summary>
        string Id { get; }

        /// <summary>Заголовок для UI.</summary>
        string Title { get; }

        /// <summary>Идентификатор иконки (опционально).</summary>
        string IconId { get; }

        /// <summary>Порядок отображения (меньше — раньше).</summary>
        int Order { get; }

        /// <summary>Алиасы для голосового поиска страницы (например, «голос», «озвучка»).</summary>
        string[] Aliases { get; }

        /// <summary>Построить страницу через view.</summary>
        void Build(ISettingsView view);

        /// <summary>Попытка обработать голосовую фразу, нормализованную к нижнему регистру.
        /// Возвращает true и кладёт ответ в reply, если фраза распознана как команда к этой странице.</summary>
        bool TryVoice(string normalized, out string reply);
    }

    /// <summary>
    /// Реестр страниц настроек. Регистрация происходит один раз при старте приложения.
    /// </summary>
    public static class SettingsRegistry
    {
        private static readonly List<ISettingsPage> _pages = new List<ISettingsPage>();
        private static bool _locked;

        /// <summary>Зарегистрировать страницу. После первого вызова BuildPages список замораживается.</summary>
        public static void Register(ISettingsPage page)
        {
            if (_locked) throw new InvalidOperationException("SettingsRegistry already locked");
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (string.IsNullOrEmpty(page.Id)) throw new ArgumentException("Page Id cannot be empty");
            _pages.Add(page);
        }

        /// <summary>Все страницы, отсортированные по Order.</summary>
        public static IReadOnlyList<ISettingsPage> Pages
        {
            get
            {
                _locked = true;
                _pages.Sort((a, b) => a.Order.CompareTo(b.Order));
                return _pages;
            }
        }

        /// <summary>Найти страницу по Id или по одному из Aliases (без учёта регистра).</summary>
        public static ISettingsPage Find(string idOrAlias)
        {
            if (string.IsNullOrEmpty(idOrAlias)) return null;
            string needle = idOrAlias.Trim().ToLowerInvariant();
            foreach (var p in _pages)
            {
                if (string.Equals(p.Id, needle, StringComparison.OrdinalIgnoreCase)) return p;
                if (p.Aliases != null)
                {
                    foreach (var a in p.Aliases)
                        if (string.Equals(a, needle, StringComparison.OrdinalIgnoreCase)) return p;
                }
            }
            return null;
        }

        /// <summary>Сбросить реестр (только для тестов).</summary>
        public static void ResetForTests()
        {
            _pages.Clear();
            _locked = false;
        }
    }
}