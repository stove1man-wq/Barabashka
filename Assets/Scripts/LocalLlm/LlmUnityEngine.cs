using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;
using LLMUnity;
using UnityEngine;

namespace Barabashka.LocalLlm
{
    /// <summary>
    /// Модель на устройстве через пакет LLMUnity (llama.cpp). Объект с компонентами LLM и LLMAgent создаётся из кода
    /// неактивным: путь к файлу модели нужно задать до Awake, иначе LLMUnity попытается стартовать без модели.
    /// Каждый запрос независим: история очищается и собирается заново из последних реплик, системный запрос кэшируется самой библиотекой.
    /// </summary>
    internal sealed class LlmUnityEngine : ILocalLlm
    {
        private const int ContextTokens = 4096;
        private const int MaxReplyTokens = 120;
        private const int MaxHistoryMessages = 4;
        private const string JsonObjectSchema = "{\"type\":\"object\"}";

        private GameObject _host;
        private LLM _llm;
        private LLMAgent _agent;
        private string _loadedPath;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private string _status = "не загружена";

        private const int LoadTimeoutMs = 90000;

        public string Status { get { return _status; } }

        public async Task<string> ChatAsync(string modelPath, string system, IList<KeyValuePair<string, string>> history,
            string userText, bool jsonMode, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(modelPath);
                ct.ThrowIfCancellationRequested();

                _agent.systemPrompt = system;
                _agent.grammar = jsonMode ? JsonObjectSchema : string.Empty;

                await _agent.ClearHistory();
                if (history != null)
                {
                    int start = Math.Max(0, history.Count - MaxHistoryMessages);
                    for (int i = start; i < history.Count; i++)
                    {
                        if (history[i].Key == "assistant") await _agent.AddAssistantMessage(history[i].Value);
                        else await _agent.AddUserMessage(history[i].Value);
                    }
                }

                _status = "думает…";
                var timer = System.Diagnostics.Stopwatch.StartNew();
                Debug.Log("[LocalLLM] Генерация ответа начата");
                string answer = await _agent.Chat(userText, null, null, false);
                Debug.Log("[LocalLLM] Генерация закончена за " + (timer.ElapsedMilliseconds / 1000f).ToString("0.0") + " с");
                _status = "готова";
                if (string.IsNullOrWhiteSpace(answer)) throw new LocalLlmException("пустой ответ модели в телефоне", false);
                return answer;
            }
            finally
            {
                _gate.Release();
                if (_status == "думает…") _status = "готова";
            }
        }

        public async Task PreloadAsync(string modelPath, string system)
        {
            await _gate.WaitAsync();
            try
            {
                await EnsureLoadedAsync(modelPath);
                _status = "прогрев…";
                var timer = System.Diagnostics.Stopwatch.StartNew();
                _agent.systemPrompt = system;
                _agent.grammar = string.Empty;
                await _agent.ClearHistory();
                await _agent.Warmup("Привет");
                Debug.Log("[LocalLLM] Прогрев системного запроса за " + (timer.ElapsedMilliseconds / 1000f).ToString("0.0") + " с");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LocalLLM] Предзагрузка не удалась: " + ex.Message);
            }
            finally
            {
                _gate.Release();
                if (_status == "прогрев…") _status = "готова";
            }
        }

        public void Unload()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
            _llm = null;
            _agent = null;
            _loadedPath = null;
            _status = "не загружена";
        }

        private async Task EnsureLoadedAsync(string modelPath)
        {
            if (_host != null && _loadedPath == modelPath && _llm != null && _llm.started) return;
            if (_host != null) Unload();

            _status = "загрузка…";
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("[LocalLLM] Загрузка модели: " + modelPath);
            _host = new GameObject("BarabashkaLocalLlm");
            _host.SetActive(false);

            _llm = _host.AddComponent<LLM>();
            _llm.model = modelPath;
            _llm.contextSize = ContextTokens;
            _llm.numGPULayers = 0;
            _llm.numThreads = Mathf.Clamp(SystemInfo.processorCount - 2, 2, 6);

            _agent = _host.AddComponent<LLMAgent>();
            _agent.llm = _llm;
            _agent.numPredict = MaxReplyTokens;
            _agent.temperature = 0.2f;
            _agent.systemPrompt = "Отвечай по-русски.";

            _host.SetActive(true);

            try
            {
                Task ready = _llm.WaitUntilReady();
                Task first = await Task.WhenAny(ready, Task.Delay(LoadTimeoutMs));
                if (first != ready)
                {
                    Unload();
                    throw new LocalLlmException("модель не загрузилась за " + (LoadTimeoutMs / 1000) + " с (не хватает памяти или мощности)", false);
                }
                await ready;
            }
            catch (LocalLlmException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Unload();
                throw new LocalLlmException("модель в телефоне не запустилась: " + ex.Message, false);
            }

            await Task.Yield();
            _loadedPath = modelPath;
            _status = "готова";
            Debug.Log("[LocalLLM] Модель загружена за " + (loadTimer.ElapsedMilliseconds / 1000f).ToString("0.0") + " с");
        }
    }

    internal static class LlmUnityEngineRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            try
            {
                LocalLlmHost.Current = new LlmUnityEngine();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LocalLLM] Не удалось подключить локальную модель: " + ex.Message);
            }
        }
    }
}
