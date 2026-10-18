using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Barabashka.Contracts;

public sealed class WeatherTool : IAgentTool
{
    public string Name { get { return "weather"; } }

    public string Description
    {
        get
        {
            return "узнать погоду. {\"tool\":\"weather\",\"city\":\"Москва\",\"day\":\"today|tomorrow|day_after|week\"}; " +
                   "city в именительном падеже, можно не указывать, если город по умолчанию уже задан.";
        }
    }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        try
        {
            string cityName = ToolArgs.Get(args, "city");
            AgentWeather.City city;
            if (cityName.Length > 0)
            {
                city = await AgentWeather.FindCityAsync(cityName, ct);
                if (city == null) return ToolResult.Fail("Не нашёл город «" + cityName + "». Попробуйте назвать его иначе.");
            }
            else
            {
                city = AgentWeather.DefaultCity();
                if (city == null)
                    return ToolResult.Fail("Скажите город: «погода в Москве». Чтобы не повторять, скажите «мой город Москва».");
            }

            return ToolResult.Ok(await AgentWeather.DescribeAsync(city, ToolArgs.Get(args, "day"), ct));
        }
        catch (HttpFailure ex)
        {
            return ToolResult.Fail("Не удалось узнать погоду: " + ex.Message + ".");
        }
    }
}

public sealed class WeatherSetCityTool : IAgentTool
{
    public string Name { get { return "set_city"; } }
    public string Description { get { return "запомнить город по умолчанию для погоды. {\"tool\":\"set_city\",\"city\":\"Москва\"}"; } }

    public async Task<ToolResult> ExecuteAsync(IDictionary<string, string> args, CancellationToken ct)
    {
        string cityName = ToolArgs.Get(args, "city");
        if (cityName.Length == 0) return ToolResult.Fail("Не услышал название города.");

        try
        {
            AgentWeather.City city = await AgentWeather.FindCityAsync(cityName, ct);
            if (city == null) return ToolResult.Fail("Не нашёл город «" + cityName + "». Попробуйте назвать его иначе.");
            AgentWeather.SaveDefaultCity(city);
            return ToolResult.Ok("Хорошо, мой город по умолчанию для погоды: " + city.Name + ".");
        }
        catch (HttpFailure ex)
        {
            return ToolResult.Fail("Не удалось проверить город: " + ex.Message + ".");
        }
    }
}
