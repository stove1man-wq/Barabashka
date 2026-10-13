using System;
using UnityEngine;

public static class AgentToolset
{
    public static string GetCurrentSystemTime()
    {
        return DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00");
    }

    public static void OpenWebBrowser(string url)
    {
        Debug.Log("[Toolset] Открытие браузера по адресу: " + url);
        Application.OpenURL(url);
    }
}
