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
            // Вызов выбора документа через системное окно
            StartCoroutine(PickDocumentAsync());
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

    private System.Collections.IEnumerator PickDocumentAsync()
    {
        var ct = new CancellationTokenSource();
        var result = MediaServices.Picker.PickAsync(MediaKind.Document, true, ct.Token);
        yield return new WaitUntil(() => result.IsCompleted);
        var items = result.Result;
        if (items != null && items.Count > 0)
        {
            foreach (var item in items)
            {
                // Копируем файл в папку InboxFolder/documents
                CopyDocumentToInbox(item);
                // Отправляем в чат карточку документа
                SendDocumentCardToChat(item);
            }
        }
    }

    private void CopyDocumentToInbox(MediaItem item)
    {
        // Имитация копирования документа в папку InboxFolder/documents
        // Проверим что это локальный файл
        if (item.Uri.StartsWith("file://"))
        {
            try
            {
                string localPath = new Uri(item.Uri).LocalPath;
                string fileName = item.Title;
                string safeFileName = MakeSafeFileName(fileName);
                string destinationPath = Path.Combine(MediaPicker.InboxFolder, "documents", safeFileName);
                
                // Создаем директорию если не существует
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                
                // Копируем файл (в реальной реализации это должно быть через Android)
                File.Copy(localPath, destinationPath, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Docs] Ошибка копирования документа: " + ex.Message);
            }
        }
    }

    private void SendDocumentCardToChat(MediaItem item)
    {
        // Создаем карточку документа для чата
        string card = MediaItem.Encode(item) + "&src=document&kind=2";
        if (_agentManager != null)
        {
            _agentManager.EnqueueSystemMessage(card, "document");
        }
    }

    private string MakeSafeFileName(string fileName)
    {
        // Убираем недопустимые символы из имени файла
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrEmpty(safeName) ? "document_" + Guid.NewGuid() : safeName;
    }
}
