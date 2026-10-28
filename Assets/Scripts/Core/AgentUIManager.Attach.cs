using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Меню кнопки «＋»: Фото, Видео, Документ. Пункты выполняют те же команды, что и голос/текст
/// («выбери фото», «выбери видео»), поэтому результат появляется в чате так же, как при команде.
/// </summary>
public partial class AgentUIManager
{
    private GameObject _attachMenu;

    private void ToggleAttachMenu()
    {
        if (_attachMenu != null && _attachMenu.activeSelf)
        {
            _attachMenu.SetActive(false);
            return;
        }
        if (_attachMenu == null) BuildAttachMenu();
        _attachMenu.SetActive(true);
    }

    private void BuildAttachMenu()
    {
        Transform parent = _inputGroup.transform;

        // Прозрачная подложка на весь экран: касание вне меню закрывает его.
        Button blocker = CreateButton("AttachMenuBlocker", parent, string.Empty, new Color(0f, 0f, 0f, 0.35f), 1);
        Place(blocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        blocker.onClick.AddListener(() => _attachMenu.SetActive(false));
        _attachMenu = blocker.gameObject;

        Image panel = CreateImage("AttachMenuPanel", blocker.transform, new Color(0.12f, 0.18f, 0.27f, 1f));
        SetRoundedSprite(panel);
        Place(panel.rectTransform, new Vector2(0.30f, 0.17f), new Vector2(0.955f, 0.37f), Vector2.zero, Vector2.zero);

        AddAttachItem(panel.transform, "Фото", 0, () => RunAttach("выбери фото"));
        AddAttachItem(panel.transform, "Видео", 1, () => RunAttach("выбери видео"));
        AddAttachItem(panel.transform, "Документ", 2, () =>
        {
            _attachMenu.SetActive(false);
            SetStatus("Документы добавим следующим этапом");
        });
    }

    private void AddAttachItem(Transform panel, string label, int index, UnityEngine.Events.UnityAction action)
    {
        Button item = CreateButton("AttachItem" + label, panel, label, new Color(0.20f, 0.30f, 0.45f, 1f), 30);
        float top = 1f - index / 3f;
        Place(item.GetComponent<RectTransform>(), new Vector2(0.04f, top - 0.30f), new Vector2(0.96f, top - 0.03f), Vector2.zero, Vector2.zero);
        item.onClick.AddListener(action);
    }

    private void RunAttach(string phrase)
    {
        _attachMenu.SetActive(false);
        if (_agentManager != null) _agentManager.SendUserMessage(phrase);
    }
}
