using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Хранилище секретов (ключи API, токены). Значения хранятся только в зашифрованном виде:
/// на Android - ключом из Android Keystore (плагин SecretVault.java), на компьютере - AES с ключом, привязанным к этой машине (базовая защита для разработки).
/// Секреты не пишутся в журналы, не попадают в подсказки моделей и показываются только маской. Вызывать из главного потока.
/// </summary>
public static class SecretStore
{
    private const string Prefix = "sec.";
    private const string AndroidTag = "k1:";
    private const string DesktopTag = "pc1:";

    private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();

    public static bool Has(string name)
    {
        return Cache.ContainsKey(name) || PlayerPrefs.HasKey(Prefix + name);
    }

    /// <summary>Сохраняет секрет. false, если безопасно сохранить не удалось (тогда ничего не записывается).</summary>
    public static bool Set(string name, string value)
    {
        if (string.IsNullOrEmpty(value)) { Remove(name); return true; }
        string encrypted = Encrypt(value);
        if (encrypted == null) return false;
        PlayerPrefs.SetString(Prefix + name, encrypted);
        PlayerPrefs.Save();
        Cache[name] = value;
        return true;
    }

    /// <summary>Секрет или пустая строка (нет секрета или его не удалось расшифровать, например после переноса на другой телефон).</summary>
    public static string Get(string name)
    {
        string cached;
        if (Cache.TryGetValue(name, out cached)) return cached;
        string stored = PlayerPrefs.GetString(Prefix + name, string.Empty);
        if (stored.Length == 0) return string.Empty;
        string plain = Decrypt(stored);
        if (plain == null) return string.Empty;
        Cache[name] = plain;
        return plain;
    }

    public static void Remove(string name)
    {
        Cache.Remove(name);
        PlayerPrefs.DeleteKey(Prefix + name);
        PlayerPrefs.Save();
    }

    private static string Encrypt(string plain)
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJNI.AttachCurrentThread();
            using (var vault = new AndroidJavaClass("com.barabashka.SecretVault"))
            {
                string blob = vault.CallStatic<string>("encrypt", plain);
                return string.IsNullOrEmpty(blob) ? null : AndroidTag + blob;
            }
#else
            using (Aes aes = Aes.Create())
            {
                aes.Key = DesktopKey();
                aes.GenerateIV();
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] cipher;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
                var all = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, all, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, all, aes.IV.Length, cipher.Length);
                return DesktopTag + Convert.ToBase64String(all);
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Secrets] Не удалось зашифровать: " + ex.Message);
            return null;
        }
    }

    private static string Decrypt(string stored)
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!stored.StartsWith(AndroidTag, StringComparison.Ordinal)) return null;
            AndroidJNI.AttachCurrentThread();
            using (var vault = new AndroidJavaClass("com.barabashka.SecretVault"))
            {
                string plain = vault.CallStatic<string>("decrypt", stored.Substring(AndroidTag.Length));
                return string.IsNullOrEmpty(plain) ? null : plain;
            }
#else
            if (!stored.StartsWith(DesktopTag, StringComparison.Ordinal)) return null;
            byte[] all = Convert.FromBase64String(stored.Substring(DesktopTag.Length));
            using (Aes aes = Aes.Create())
            {
                int ivLength = aes.BlockSize / 8;
                if (all.Length <= ivLength) return null;
                var iv = new byte[ivLength];
                Buffer.BlockCopy(all, 0, iv, 0, ivLength);
                aes.Key = DesktopKey();
                aes.IV = iv;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(all, ivLength, all.Length - ivLength));
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Secrets] Не удалось расшифровать: " + ex.Message);
            return null;
        }
    }

#if !(UNITY_ANDROID && !UNITY_EDITOR)
    private static byte[] DesktopKey()
    {
        using (SHA256 sha = SHA256.Create())
            return sha.ComputeHash(Encoding.UTF8.GetBytes("barabashka|" + SystemInfo.deviceUniqueIdentifier));
    }
#endif
}
