using Barabashka.Contracts;
using Barabashka.Media;
using UnityEngine;

namespace Barabashka.Composition
{
    /// <summary>
    /// Соединяет блок просмотра медиа с остальным приложением: кладёт просмотрщик и выбор файлов в MediaServices.
    /// Ядро и чат видят только интерфейсы из Contracts. Пока блок не подключён, агент честно говорит, что просмотр недоступен.
    /// </summary>
    public static class MediaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            try
            {
                if (MediaServices.Viewer == null) MediaServices.Viewer = MediaFactory.CreateViewer();
                if (MediaServices.Picker == null) MediaServices.Picker = MediaFactory.CreatePicker();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Media] Не удалось запустить просмотр медиа: " + ex.Message);
            }
        }
    }
}
