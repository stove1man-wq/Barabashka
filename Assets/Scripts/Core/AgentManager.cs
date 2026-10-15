using System;
using System.Threading;
using Barabashka.Contracts;
using UnityEngine;

public class AgentManager : MonoBehaviour
{
    public string testInputMessage;

    private IBrain _brain;
    private AgentCommandRouter _router;
    private AgentUIManager _uiManager;
    private CancellationTokenSource _cts;
    private int _requestVersion;
    private bool _requestRunning;

    private void Awake()
    {
        _brain = new LegacyBrainAdapter(new IsolatedBrain());
        _router = GetComponent<AgentCommandRouter>();
        _uiManager = GetComponent<AgentUIManager>();

        if (_router == null)
            Debug.LogError("[AgentManager] Компонент AgentCommandRouter не найден!");
    }

    private void Start()
    {
        Debug.Log("[AgentManager] Изолированное ядро и UI успешно инициализированы.");
    }

    private void OnDestroy()
    {
        if (_cts != null) _cts.Cancel();
    }

    /// <summary>Текстовый ввод из UI. Сохранён для совместимости.</summary>
    public void SendUserMessage(string message)
    {
        SendUtterance(new Utterance(message, null, true, InputSource.Text));
    }

    /// <summary>
    /// Единая точка входа для любого канала ввода (текст, голос).
    /// Промежуточные реплики (IsFinal=false) только показываются в статусе.
    /// Вызывать только из главного потока (из фонового - через IMainThread.Post).
    /// </summary>
    /// <summary>
    /// Temporary diagnostic mode: display Whisper transcription without executing commands.
    /// </summary>
    public void ShowRecognizedVoiceText(Utterance utterance)
    {
        if (utterance == null || _uiManager == null || string.IsNullOrWhiteSpace(utterance.Text))
            return;

        if (!utterance.IsFinal)
        {
            _uiManager.SetStatus("Recognizing: " + utterance.Text);
            return;
        }

        _uiManager.AddUserMessage(utterance.Text);
        _uiManager.SetStatus("Transcription test mode: voice command not executed");
        Debug.Log("[Whisper Transcription]: " + utterance.Text);
    }
    public async void SendUtterance(Utterance utterance)
    {
        if (utterance == null || _brain == null || _router == null) return;

        if (!utterance.IsFinal)
        {
            if (_uiManager != null && !string.IsNullOrWhiteSpace(utterance.Text))
                _uiManager.SetStatus("Слышу: " + utterance.Text);
            return;
        }

        string message = utterance.Text;
        if (string.IsNullOrWhiteSpace(message)) return;

        int requestVersion = ++_requestVersion;
        _requestRunning = true;

        if (_cts != null) _cts.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        if (_uiManager != null)
        {
            _uiManager.AddUserMessage(message);
            _uiManager.SetStatus("Обрабатываю запрос…");
        }

        Debug.Log("[User Input]: " + message);
        try
        {
            AgentResponse response = await _brain.RespondAsync(utterance, token);
            if (requestVersion != _requestVersion) return;

            // Роутер пока принимает строку: команды с префиксом COMMAND:, остальное - обычный ответ.
            string routed = string.IsNullOrEmpty(response.Command)
                ? response.Text
                : "COMMAND:" + response.Command;

            Debug.Log("[Brain Response]: " + routed);
            _router.RouteCommand(routed);
            if (_uiManager != null) _uiManager.SetStatus("Готово");
        }
        catch (OperationCanceledException)
        {
            // Запрос отменён (Stop или новый запрос) - ничего не показываем.
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

        // Токен отменяется, но старый IsolatedBrain отмену не поддерживает, поэтому
        // результат дополнительно отбрасывается через номер версии запроса.
        if (_cts != null) _cts.Cancel();
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
