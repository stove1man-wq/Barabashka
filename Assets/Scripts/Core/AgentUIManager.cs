using UnityEngine;
using UnityEngine.UI;

public class AgentUIManager : MonoBehaviour
{
    private AgentManager _agentManager;
    private Text _responseText;
    private InputField _inputField;

    private void Start()
    {
        _agentManager = GetComponent<AgentManager>();

        // 1. Создаем Canvas
        GameObject canvasObj = new GameObject("Agent_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Проверяем и создаем EventSystem под новый Input System
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            
            // Вместо StandaloneInputModule используем InputSystemUIInputModule
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // 3. Создаем панель фона для текста ответа (чтобы белый текст был виден на любом фоне)
        GameObject textBgObj = new GameObject("Response_BG");
        textBgObj.transform.SetParent(canvasObj.transform);
        Image textBgImage = textBgObj.AddComponent<Image>();
        textBgImage.color = new Color(0f, 0f, 0f, 0.5f); // Полупрозрачный чёрный фон
        RectTransform textBgRect = textBgObj.GetComponent<RectTransform>();
        textBgRect.anchoredPosition = new Vector2(0, 100);
        textBgRect.sizeDelta = new Vector2(620, 220);

        // 3. Создаем текст ответа по центру (на переднем плане фона)
        GameObject textObj = new GameObject("Response_Text");
        textObj.transform.SetParent(textBgObj.transform); // Текст — ребёнок фона
        _responseText = textObj.AddComponent<Text>();
        _responseText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _responseText.fontSize = 24;
        _responseText.color = Color.white; // Белый текст — читаем на тёмном фоне
        _responseText.alignment = TextAnchor.MiddleCenter;
        _responseText.text = "Привет! Напиши мне что-нибудь в поле ввода внизу.";
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(600, 200);

        // 4. Создаем поле ввода InputField (совмещаем Image и InputField на одном объекте)
        GameObject inputObj = new GameObject("User_InputField");
        inputObj.transform.SetParent(canvasObj.transform);
        
        // Сначала добавляем Image, чтобы он стал Target Graphic для InputField
        Image bgImage = inputObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        _inputField = inputObj.AddComponent<InputField>();
        _inputField.targetGraphic = bgImage; // Явно связываем графику

        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchoredPosition = new Vector2(0, -200);
        inputRect.sizeDelta = new Vector2(400, 40);

        // Текст внутри поля ввода
        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform);
        Text inputText = inputTextObj.AddComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputText.color = Color.white;
        inputText.fontSize = 18;
        inputText.alignment = TextAnchor.MiddleLeft;
        
        RectTransform inputTextRect = inputTextObj.GetComponent<RectTransform>();
        inputTextRect.anchoredPosition = Vector2.zero;
        inputTextRect.sizeDelta = new Vector2(380, 30);
        
        _inputField.textComponent = inputText;

        // Отправка по нажатию Enter
        _inputField.onEndEdit.AddListener(delegate { SubmitText(); });
    }

    private void SubmitText()
    {
        if (string.IsNullOrEmpty(_inputField.text)) return;

        if (_agentManager != null)
        {
            _agentManager.SendUserMessage(_inputField.text);
        }
        _inputField.text = "";
        _inputField.ActivateInputField();
    }

    public void UpdateResponseText(string text)
    {
        if (_responseText != null)
        {
            _responseText.text = text;
        }
    }
}