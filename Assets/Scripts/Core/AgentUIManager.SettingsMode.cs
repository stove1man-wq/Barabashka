using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Реализация режима настроек в чате вместо отдельного окна.
/// </summary>
public partial class AgentUIManager
{
    /// <summary>
    /// Публичный метод для открытия режима настроек.
    /// </summary>
    public void ShowSettings(string pageId = null)
    {
        AppSettings.Configured = true;
        
        if (_settingsMode) return;
        
        _settingsMode = true;
        
        // Скрыть обычные кнопки разделов
        for (int i = 0; i < ChatKinds.Count + 1; i++)
        {
            if (_tabButtons[i] != null)
                _tabButtons[i].gameObject.SetActive(false);
        }
        
        // Показать кнопку назад (если она существует)
        if (_settingsBackButtonObject != null)
            _settingsBackButtonObject.SetActive(true);
        
        // Показать панель настроек если она есть
        if (_settingsPanel != null)
            _settingsPanel.gameObject.SetActive(true);
            
        // Сбросить содержимое
        ClearSettingsContent();
        
        // Отобразить первую страницу настроек
        ShowSettingsPage(pageId);
    }
    
    /// <summary>
    /// Выход из режима настроек.
    /// </summary>
    private void ExitSettingsMode()
    {
        if (!_settingsMode) return;
        
        _settingsMode = false;
        _currentSettingsPageId = null;
        
        // Показать обычные кнопки разделов
        for (int i = 0; i < ChatKinds.Count + 1; i++)
        {
            if (_tabButtons[i] != null)
                _tabButtons[i].gameObject.SetActive(true);
        }
        
        // Скрыть кнопку назад
        if (_settingsBackButtonObject != null)
            _settingsBackButtonObject.SetActive(false);
        
        // Скрыть панель настроек
        if (_settingsPanel != null)
            _settingsPanel.gameObject.SetActive(false);
        
        ClearSettingsContent();
        
        // Восстановить фильтр
        ApplyFilter();
    }
    
    private void ShowSettingsPage(string pageId)
    {
        ClearSettingsContent();
        
        var page = string.IsNullOrEmpty(pageId) 
            ? SettingsRegistry.Pages.Count > 0 ? SettingsRegistry.Pages[0] : null 
            : SettingsRegistry.Find(pageId);
            
        if (page == null)
        {
            AddSystemMessage("Страница настроек не найдена");
            return;
        }
        
        _currentSettingsPageId = page.Id;
        
        // Отобразить заголовок
        if (_settingsTitleText != null)
            _settingsTitleText.text = page.Title;
        
        // Создать вид для этой страницы
        var view = new SettingsView(this);
        page.Build(view);
        
        // Прокрутить к верху
        if (_settingsScroll != null)
        {
            _settingsScroll.normalizedPosition = new Vector2(0, 1);
        }
    }
    
    private void ClearSettingsContent()
    {
        foreach (var widget in _settingsWidgets)
        {
            if (widget != null)
                Destroy(widget);
        }
        _settingsWidgets.Clear();
        
        // Очистить содержимое
        if (_settingsContentArea != null)
        {
            foreach (Transform child in _settingsContentArea)
            {
                if (child.gameObject != _settingsBackButtonObject) // Не удаляем кнопку назад
                    Destroy(child.gameObject);
            }
        }
    }
    
    // Вспомогательный класс для отображения настроек в чате
    private sealed class SettingsView : ISettingsView
    {
        private readonly AgentUIManager _manager;
        
        public SettingsView(AgentUIManager manager)
        {
            _manager = manager;
        }
        
        public void Section(string title)
        {
            _manager.AddSettingsSection(title);
        }
        
        public void Hint(string text)
        {
            _manager.AddSettingsHint(text);
        }
        
        public void Toggle(string title, Func<bool> get, Func<bool, string> set)
        {
            _manager.AddSettingsToggle(title, get, set);
        }
        
        public void Choice(string title, string[] options, Func<int> get, Func<int, string> set)
        {
            _manager.AddSettingsChoice(title, options, get, set);
        }
        
        public void Input(string caption, string placeholder, bool secret, Func<string> get, Func<string, string> set)
        {
            _manager.AddSettingsInput(caption, placeholder, secret, get, set);
        }
    }
    
    private void AddSettingsSection(string title)
    {
        // Простая реализация для демонстрации
        AddSystemMessage($"Section: {title}");
    }
    
    private void AddSettingsHint(string text)
    {
        // Простая реализация для демонстрации
        AddSystemMessage($"Hint: {text}");
    }
    
    private void AddSettingsToggle(string title, Func<bool> get, Func<bool, string> set)
    {
        // Простая реализация для демонстрации
        AddSystemMessage($"Toggle: {title}");
    }
    
    private void AddSettingsChoice(string title, string[] options, Func<int> get, Func<int, string> set)
    {
        // Простая реализация для демонстрации
        AddSystemMessage($"Choice: {title}");
    }
    
    private void AddSettingsInput(string caption, string placeholder, bool secret, Func<string> get, Func<string, string> set)
    {
        // Простая реализация для демонстрации
        AddSystemMessage($"Input: {caption}");
    }
}