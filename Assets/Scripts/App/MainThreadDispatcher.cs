using System;
using System.Collections.Concurrent;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.App
{
    /// <summary>
    /// Возвращает вызовы из фоновых потоков в главный поток Unity.
    /// Создаётся сам при старте приложения, на сцену ничего добавлять не нужно.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour, IMainThread
    {
        private static MainThreadDispatcher _instance;
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static IMainThread Instance
        {
            get { return _instance; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null) return;
            var go = new GameObject("[MainThreadDispatcher]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThreadDispatcher>();
        }

        public void Post(Action action)
        {
            if (action == null) return;
            _queue.Enqueue(action);
        }

        private void Update()
        {
            Action action;
            while (_queue.TryDequeue(out action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
