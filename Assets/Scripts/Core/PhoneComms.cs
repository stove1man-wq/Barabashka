using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Звонки, SMS и чаты в мессенджерах. Звонок и SMS можно сделать полностью самим (нужны разрешения, Android спросит их при первом использовании);
/// Telegram и WhatsApp не дают отправить сообщение за человека, поэтому для них открывается чат с готовым текстом.
/// </summary>
public static class PhoneComms
{
    public const string PermContacts = "android.permission.READ_CONTACTS";
    public const string PermCall = "android.permission.CALL_PHONE";
    public const string PermSms = "android.permission.SEND_SMS";

    /// <summary>Номер только из «+» и цифр.</summary>
    public static string CleanNumber(string raw)
    {
        var sb = new StringBuilder();
        string s = raw ?? string.Empty;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsDigit(c) || (c == '+' && sb.Length == 0)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Номер для ссылок мессенджеров: только цифры с кодом страны (8900… и 900… превращаются в 7900…).</summary>
    public static string MessengerNumber(string raw)
    {
        string d = CleanNumber(raw).TrimStart('+');
        if (d.Length == 11 && d[0] == '8') return "7" + d.Substring(1);
        if (d.Length == 10 && d[0] == '9') return "7" + d;
        return d;
    }

    /// <summary>Выдано ли разрешение (на ПК считается выданным).</summary>
    public static bool HasPermission(string permission)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(permission);
#else
        return true;
#endif
    }

    public static async Task<bool> EnsurePermissionAsync(string permission)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(permission)) return true;
        var tcs = new TaskCompletionSource<bool>();
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => tcs.TrySetResult(true);
        callbacks.PermissionDenied += _ => tcs.TrySetResult(false);
        callbacks.PermissionDeniedAndDontAskAgain += _ => tcs.TrySetResult(false);
        Permission.RequestUserPermissions(new[] { permission }, callbacks);
        Task finished = await Task.WhenAny(tcs.Task, Task.Delay(60000));
        return finished == tcs.Task && tcs.Task.Result;
#else
        await Task.Yield();
        return true;
#endif
    }

    // ---- звонок ----

    /// <summary>Открыть набор номера: звонок начнёт сам человек.</summary>
    public static string OpenDialer(string number, string who)
    {
        string n = CleanNumber(number);
        Application.OpenURL("tel:" + n);
        return "Открываю набор номера" + (string.IsNullOrEmpty(who) ? "" : " (" + who + ")") + ". Нажмите вызов, чтобы позвонить.";
    }

    /// <summary>Позвонить сразу. Нет разрешения или ошибка - откроется обычный набор номера.</summary>
    public static async Task<string> CallNowAsync(string number, string who)
    {
        string label = string.IsNullOrEmpty(who) ? CleanNumber(number) : who;
        if (!await EnsurePermissionAsync(PermCall))
            return "Нет разрешения на звонки. " + OpenDialer(number, who);
        try
        {
            StartCall(CleanNumber(number));
            return "Звоню: " + label + ".";
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Phone] Звонок: " + ex.Message);
            return "Не получилось позвонить сразу (" + ex.Message + "). " + OpenDialer(number, who);
        }
    }

    // ---- SMS ----

    /// <summary>Открыть окно нового сообщения с готовым текстом: отправит сам человек.</summary>
    public static string OpenSmsComposer(string number, string text, string who)
    {
        string n = CleanNumber(number);
        string target = string.IsNullOrEmpty(who) ? n : who;
        if (!StartSmsComposer(n, text ?? string.Empty))
            return "Не удалось открыть окно сообщения.";
        return "Открываю сообщение для " + target + (string.IsNullOrEmpty(text) ? "." : ": текст готов, нажмите «отправить».");
    }

    /// <summary>Отправить SMS сразу. Нет разрешения или ошибка - откроется окно сообщения с готовым текстом.</summary>
    public static async Task<string> SendSmsNowAsync(string number, string text, string who)
    {
        string n = CleanNumber(number);
        string target = string.IsNullOrEmpty(who) ? n : who;
        if (!await EnsurePermissionAsync(PermSms))
            return "Нет разрешения на отправку SMS. " + OpenSmsComposer(number, text, who);
        try
        {
            SendSmsPlatform(n, text);
            return "Отправил SMS для " + target + ": «" + text + "».";
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Phone] SMS: " + ex.Message);
            return "Не получилось отправить сразу (" + ex.Message + "). " + OpenSmsComposer(number, text, who);
        }
    }

    // ---- Telegram и WhatsApp ----

    public static string OpenMessengerChat(string via, string number, string text, string who)
    {
        string digits = MessengerNumber(number);
        if (digits.Length < 7) return "Не разобрал номер получателя.";
        string target = string.IsNullOrEmpty(who) ? "+" + digits : who;
        string escaped = Uri.EscapeDataString(text ?? string.Empty);
        bool telegram = via == "telegram";

        string url = telegram
            ? "tg://resolve?phone=" + digits + (escaped.Length > 0 ? "&text=" + escaped : string.Empty)
            : "https://wa.me/" + digits + (escaped.Length > 0 ? "?text=" + escaped : string.Empty);
        string[] packages = telegram
            ? new[] { "org.telegram.messenger", "org.telegram.messenger.web", null }
            : new[] { "com.whatsapp", "com.whatsapp.w4b", null };
        string app = telegram ? "Телеграм" : "WhatsApp";

        if (!StartView(url, packages))
            return app + " не найден на телефоне.";
        return "Открыл чат с " + target + " в приложении " + app + (string.IsNullOrEmpty(text) ? "." : ": текст готов, нажмите «отправить».") +
               " Отправить за вас Android не позволяет.";
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private const int FlagNewTask = 0x10000000;

    private static AndroidJavaObject CurrentActivity()
    {
        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            return player.GetStatic<AndroidJavaObject>("currentActivity");
    }

    private static void StartIntent(string action, string uriString, string package, string bodyExtra)
    {
        using (AndroidJavaObject activity = CurrentActivity())
        using (var intent = new AndroidJavaObject("android.content.Intent", action))
        using (var uriClass = new AndroidJavaClass("android.net.Uri"))
        using (AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>("parse", uriString))
        {
            intent.Call<AndroidJavaObject>("setData", uri);
            if (!string.IsNullOrEmpty(package)) intent.Call<AndroidJavaObject>("setPackage", package);
            if (bodyExtra != null) intent.Call<AndroidJavaObject>("putExtra", "sms_body", bodyExtra);
            intent.Call<AndroidJavaObject>("addFlags", FlagNewTask);
            activity.Call("startActivity", intent);
        }
    }

    private static void StartCall(string number)
    {
        StartIntent("android.intent.action.CALL", "tel:" + number, null, null);
    }

    private static bool StartSmsComposer(string number, string text)
    {
        try
        {
            StartIntent("android.intent.action.SENDTO", "smsto:" + number, null, text);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Phone] Окно SMS: " + ex.Message);
            return false;
        }
    }

    private static bool StartView(string url, string[] packages)
    {
        foreach (string package in packages)
        {
            try
            {
                StartIntent("android.intent.action.VIEW", url, package, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Phone] Открытие " + (package ?? "любым приложением") + ": " + ex.Message);
            }
        }
        return false;
    }

    // SmsManager.sendMultipartTextMessage принимает null (отчёты о доставке не нужны), а Unity не умеет передавать null в Call, поэтому прямой вызов JNI.
    private static void SendSmsPlatform(string number, string text)
    {
        using (var smsClass = new AndroidJavaClass("android.telephony.SmsManager"))
        using (AndroidJavaObject manager = smsClass.CallStatic<AndroidJavaObject>("getDefault"))
        using (AndroidJavaObject parts = manager.Call<AndroidJavaObject>("divideMessage", text))
        {
            IntPtr obj = manager.GetRawObject();
            IntPtr cls = AndroidJNI.GetObjectClass(obj);
            IntPtr dest = IntPtr.Zero;
            try
            {
                IntPtr method = AndroidJNI.GetMethodID(cls, "sendMultipartTextMessage",
                    "(Ljava/lang/String;Ljava/lang/String;Ljava/util/ArrayList;Ljava/util/ArrayList;Ljava/util/ArrayList;)V");
                dest = AndroidJNI.NewStringUTF(number);
                var args = new jvalue[5];
                args[0].l = dest;
                args[1].l = IntPtr.Zero;
                args[2].l = parts.GetRawObject();
                args[3].l = IntPtr.Zero;
                args[4].l = IntPtr.Zero;
                AndroidJNI.CallVoidMethod(obj, method, args);
            }
            finally
            {
                if (dest != IntPtr.Zero) AndroidJNI.DeleteLocalRef(dest);
                AndroidJNI.DeleteLocalRef(cls);
            }
        }
    }
#else
    private static void StartCall(string number) { throw new NotSupportedException("звонки работают только на телефоне"); }
    private static bool StartSmsComposer(string number, string text) { return false; }
    private static void SendSmsPlatform(string number, string text) { throw new NotSupportedException("SMS отправляются только с телефона"); }

    private static bool StartView(string url, string[] packages)
    {
        if (url.StartsWith("http", StringComparison.Ordinal)) { Application.OpenURL(url); return true; }
        return false;
    }
#endif
}
