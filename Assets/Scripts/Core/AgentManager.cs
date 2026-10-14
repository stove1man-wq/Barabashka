using System;
using UnityEngine;

public class AgentManager : MonoBehaviour
{
    public string testInputMessage;

    private IAgentBrain _brain;
    private AgentCommandRouter _router;
    private AgentUIManager _uiManager;
    private int _requestVersion;
    private bool _requestRunning;

    private void Awake()
    {
        _brain = new IsolatedBrain();
        _router = GetComponent<AgentCommandRouter>();
        _uiManager = GetComponent<AgentUIManager>();

        if (_router == null)
            Debug.LogError("[AgentManager] Компонент AgentCommandRouter не найден!");
    }

    private void Start()
    {
        Debug.Log("[AgentManager] Изолированное ядро и UI успешно инициализированы.");
    }

    public async void SendUserMessage(string message)
    {
        if (_brain == null || _router == null || string.IsNullOrWhiteSpace(message)) return;

        int requestVersion = ++_requestVersion;
        _requestRunning = true;
        if (_uiManager != null)
        {
            _uiManager.AddUserMessage(message);
            _uiManager.SetStatus("Обрабатываю запрос…");
        }

        Debug.Log("[User Input]: " + message);
        try
        {
            string response = await _brain.ProcessMessageAsync(message);
            if (requestVersion != _requestVersion) return;

            Debug.Log("[Brain Response]: " + response);
            _router.RouteCommand(response);
            if (_uiManager != null) _uiManager.SetStatus("Готово");
        }
        catch (Exception ex)
        {
            if (requestVersion != _requestVersion) return;
            Debug.LogException(ex);
            if (_uiManager != null)
            {
                _uiManager.AddAgentMessage("Не удалось обработать запрос. Подробности записаны в журнале Unity.");
                _uiManager.SetStatus("Ошибка");
            }
        }
        finally
        {
            if (requestVersion == _requestVersion) _requestRunning = false;
        }
    }

    public void StopCurrentTask()
    {
        if (!_requestRunning)
        {
            if (_uiManager != null) _uiManager.SetStatus("Нет активной задачи");
            return;
        }

        // Ignore the result of the in-flight operation so it cannot overwrite the chat.
        // True cancellation requires cancellation support in the active IAgentBrain provider.
        _requestVersion++;
        _requestRunning = false;
        if (_uiManager != null)
        {
            _uiManager.AddAgentMessage("Запрос остановлен в интерфейсе. Если обработчик не поддерживает отмену, вычисление может завершиться в фоне.");
            _uiManager.SetStatus("Остановлено");
        }
        Debug.Log("[AgentManager] Current response invalidated by Stop.");
    }

    [ContextMenu("Send Test Message")]
    public void SendInspectorMessage()
    {
        if (!string.IsNullOrEmpty(testInputMessage))
            SendUserMessage(testInputMessage);
    }
}