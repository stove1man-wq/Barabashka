using System;
using UnityEngine;

public class AgentManager : MonoBehaviour
{
    public string testInputMessage;
    
    private IAgentBrain _brain;
    private AgentCommandRouter _router;
    private AgentUIManager _uiManager;

    private void Awake()
    {
        _brain = new IsolatedBrain();
        _router = GetComponent<AgentCommandRouter>();
        _uiManager = GetComponent<AgentUIManager>();

        if (_router == null)
        {
            Debug.LogError("[AgentManager] Компонент AgentCommandRouter не найден!");
        }
    }

    private void Start()
    {
        Debug.Log("[AgentManager] Изолированное ядро и UI успешно инициализированы.");
        if (_uiManager != null)
        {
            _uiManager.UpdateResponseText("Привет! Напиши мне что-нибудь в поле ввода внизу.");
        }
    }

    public async void SendUserMessage(string message)
    {
        if (_brain == null || _router == null) return;

        Debug.Log("[User Input]: " + message);
        string response = await _brain.ProcessMessageAsync(message);
        
        Debug.Log("[Brain Response]: " + response);
        
        // Router сам решит, что показывать в UI (в том числе время, ошибки и т.д.)
        if (_router != null) _router.RouteCommand(response);
        // НЕ вызываем _uiManager.UpdateResponseText(response) здесь — это создаёт рассинхрон
    }

    [ContextMenu("Send Test Message")]
    public void SendInspectorMessage()
    {
        if (!string.IsNullOrEmpty(testInputMessage))
        {
            SendUserMessage(testInputMessage);
        }
    }
}