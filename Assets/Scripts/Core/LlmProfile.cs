using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Один вариант подключения к языковой модели: локальная Ollama или любой OpenAI-совместимый сервис.</summary>
[Serializable]
public class LlmProfile
{
    public string name;
    public string kind;     // "ollama", "openai", "device" (модель на самом устройстве, model - путь к файлу), "gigachat"
    public string url;      // для openai - адрес до /v1, например https://openrouter.ai/api/v1
    public string model;
    public string apiKey;
    public string project;  // ID каталога Яндекса для YandexGPT
    
    public bool IsDevice
    {
        get { return string.Equals(kind, "device", StringComparison.OrdinalIgnoreCase); }
    }

    public bool IsOpenAi
    {
        get { return string.Equals(kind, "openai", StringComparison.OrdinalIgnoreCase); }
    }
    
    public bool IsGigaChat
    {
        get { return string.Equals(kind, "gigachat", StringComparison.OrdinalIgnoreCase); }
    }
    
    public bool IsYandexGpt
    {
        get { return string.Equals(kind, "yandex", StringComparison.OrdinalIgnoreCase); }
    }
}