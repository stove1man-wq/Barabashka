using System;
using System.Runtime.InteropServices;

/// <summary>
/// Вызовы нативной библиотеки TDLib (libtdjson.so, официальный JSON-интерфейс Telegram).
/// На Android библиотека кладётся в Assets/Plugins/Android/libs/arm64-v8a/ (собирается workflow «Build TDLib Android», см. docs/TELEGRAM.md).
/// </summary>
internal static class TdJson
{
    private const string Lib = "tdjson";

    [DllImport(Lib)] public static extern int td_create_client_id();
    [DllImport(Lib)] public static extern void td_send(int clientId, [MarshalAs(UnmanagedType.LPUTF8Str)] string request);
    [DllImport(Lib)] public static extern IntPtr td_receive(double timeout);
    [DllImport(Lib)] public static extern IntPtr td_execute([MarshalAs(UnmanagedType.LPUTF8Str)] string request);

    /// <summary>Строка из указателя, который вернула библиотека (указатель действует до следующего td_receive).</summary>
    public static string ReadString(IntPtr pointer)
    {
        return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);
    }
}
