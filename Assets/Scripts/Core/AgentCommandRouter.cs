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
        _uiManager = GetComponent<AgentUIManager>();
        _avatar = GetComponent<AvatarVisualizer>();
    }

    public void RouteCommand(string brainResponse)
    {
        if (string.IsNullOrEmpty(brainResponse))
        {
            PlayAnimation("Idle");
            return;
        }

        if (brainResponse.StartsWith("COMMAND:", StringComparison.Ordinal))
        {
            string command = brainResponse.Substring("COMMAND:".Length);
            if (command == "TIME")
            {
                ShowResponse("Текущее точное время: " + AgentToolset.GetCurrentSystemTime());
                PlayAnimation("LookUp");
            }
            else if (command.StartsWith("OPEN_URL|", StringComparison.Ordinal))
            {
                string url = command.Substring("OPEN_URL|".Length);
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri parsed) &&
                    (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
                {
                    AgentToolset.OpenWebBrowser(parsed.AbsoluteUri);
                    ShowResponse("Открываю браузер: " + parsed.Host);
                    PlayAnimation("Greet");
                }
                else ShowResponse("Не удалось открыть ссылку: допустимы только HTTP и HTTPS.");
            }
            else if (command.StartsWith("TASK_ADD|", StringComparison.Ordinal))
                ShowResponse(AgentToolset.AddTask(command.Substring("TASK_ADD|".Length)));
            else if (command == "TASK_LIST")
                ShowResponse(AgentToolset.ListTasks());
            else if (command.StartsWith("TASK_DONE|", StringComparison.Ordinal))
                ShowResponse(AgentToolset.CompleteTask(command.Substring("TASK_DONE|".Length)));
            else if (command.StartsWith("TASK_DELETE|", StringComparison.Ordinal))
                ShowResponse(AgentToolset.DeleteTask(command.Substring("TASK_DELETE|".Length)));
            else
                ShowResponse("Команда не поддерживается: " + command);

            PlayAnimation("Idle");
            return;
        }

        ShowResponse(brainResponse);
        if (brainResponse.IndexOf("привет", StringComparison.OrdinalIgnoreCase) >= 0 ||
            brainResponse.IndexOf("рад помочь", StringComparison.OrdinalIgnoreCase) >= 0)
            PlayAnimation("Greet");
        else if (brainResponse.IndexOf("погода", StringComparison.OrdinalIgnoreCase) >= 0)
            PlayAnimation("LookUp");
        else
            PlayAnimation("Idle");
    }

    private void ShowResponse(string text)
    {
        if (_uiManager == null) _uiManager = GetComponent<AgentUIManager>();
        if (_uiManager != null) _uiManager.UpdateResponseText(text);
        else Debug.LogWarning("[AgentCommandRouter] AgentUIManager is unavailable. Response: " + text);
    }

    private void PlayAnimation(string animName)
    {
        if (_avatar != null)
        {
            switch (animName)
            {
                case "LookUp": _avatar.TriggerLookUp(); break;
                case "Greet": _avatar.TriggerGreet(); break;
                case "Idle": _avatar.TriggerIdle(); break;
            }
        }
        Debug.Log("[Avatar] Play animation: " + animName);
    }
}
