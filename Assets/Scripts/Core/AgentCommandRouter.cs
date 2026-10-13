using System;
using UnityEngine;

public class AgentCommandRouter : MonoBehaviour
{
    private AgentUIManager _uiManager;
    private AvatarVisualizer _avatar;

    private void Awake()
    {
        _avatar = GetComponent<AvatarVisualizer>();
    }

    private void Start()
    {
        // Инициализируем UI менеджер в Start, чтобы кэш Bootstrap гарантированно успел собраться
        _uiManager = GetComponent<AgentUIManager>();
    }

    public void RouteCommand(string brainResponse)
    {
        if (string.IsNullOrEmpty(brainResponse))
        {
            PlayAnimation("Idle");
            return;
        }

        // Перехват системных команд Тулсета
        if (brainResponse.StartsWith("COMMAND:"))
        {
            if (brainResponse == "COMMAND:TIME")
            {
                string currentTime = AgentToolset.GetCurrentSystemTime();
                Debug.Log("[Agent] Текущее системное время: " + currentTime);
                
                // Прямой вывод на экран через закэшированную ссылку
                if (_uiManager != null)
                {
                    _uiManager.UpdateResponseText("Текущее точное время: " + currentTime);
                }
                else
                {
                    // Подстраховка, если компонент искался медленно
                    _uiManager = GetComponent<AgentUIManager>();
                    if (_uiManager != null) _uiManager.UpdateResponseText("Текущее точное время: " + currentTime);
                }

                PlayAnimation("LookUp");
            }
            else if (brainResponse.StartsWith("COMMAND:OPEN_URL|"))
            {
                string[] parts = brainResponse.Split('|');
                if (parts.Length > 1)
                {
                    string url = parts[1];
                    AgentToolset.OpenWebBrowser(url);
                    PlayAnimation("Greet");
                }
            }
            return;
        }

        // Стандартные текстовые ответы изолированного мозга
        if (brainResponse.IndexOf("привет", StringComparison.OrdinalIgnoreCase) >= 0 ||
            brainResponse.IndexOf("рад помочь", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Выводим ответ мозга на экран
            if (_uiManager != null) _uiManager.UpdateResponseText(brainResponse);
            PlayAnimation("Greet");
        }
        else if (brainResponse.IndexOf("погода", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // Выводим ответ мозга на экран
            if (_uiManager != null) _uiManager.UpdateResponseText(brainResponse);
            PlayAnimation("LookUp");
        }
        else
        {
            // Выводим ответ мозга на экран
            if (_uiManager != null) _uiManager.UpdateResponseText(brainResponse);
            PlayAnimation("Idle");
        }
    }

    private void PlayAnimation(string animName)
    {
        // Вызываем соответствующий метод аватара (корутины)
        if (_avatar != null)
        {
            switch (animName)
            {
                case "LookUp": _avatar.TriggerLookUp(); break;
                case "Greet":  _avatar.TriggerGreet(); break;
                case "Idle":   _avatar.TriggerIdle(); break;
            }
        }
        Debug.Log("[Avatar] Play animation: " + animName);
    }
}
