using System;
using System.Threading.Tasks;

public class IsolatedBrain : IAgentBrain
{
    public Task<string> ProcessMessageAsync(string userText)
    {
        if (string.IsNullOrEmpty(userText))
        {
            return Task.FromResult("Я вас не расслышал. Повторите, пожалуйста.");
        }

        if (userText.IndexOf("привет", StringComparison.OrdinalIgnoreCase) >= 0 ||
            userText.IndexOf("здравствуй", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Task.FromResult("Приветствую! Я ваш независимый ИИ-агент. Рад помочь!");
        }

        if (userText.IndexOf("погода", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Task.FromResult("Сегодня отличная погода для разработки локальных систем!");
        }

        if (userText.IndexOf("время", StringComparison.OrdinalIgnoreCase) >= 0 ||
            userText.IndexOf("час", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Task.FromResult("COMMAND:TIME");
        }

        if (userText.IndexOf("браузер", StringComparison.OrdinalIgnoreCase) >= 0 ||
            userText.IndexOf("гитхаб", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Task.FromResult("COMMAND:OPEN_URL|https://github.com");
        }

        return Task.FromResult("Команда принята, но в режиме полной изоляции этот сценарий еще не настроен.");
    }
}