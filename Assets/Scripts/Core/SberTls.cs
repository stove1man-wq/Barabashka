using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// TLS для серверов Сбера (GigaChat): сертификат сервера проверяется по корневому сертификату НУЦ Минцифры,
/// файлы лежат в Assets/Resources/certs. Общую проверку сертификатов не отключаем.
/// </summary>
public static class SberTls
{
    public const string MissingMessage = "Для GigaChat нужны сертификаты Минцифры в Assets/Resources/certs (см. README.txt там)";
    private const string RootResource = "certs/mincifry_root";
    private const string SubResource = "certs/mincifry_sub";

    /// <summary>Обработчик для запроса к хосту Сбера или null, если корневого сертификата нет в сборке.</summary>
    public static CertificateHandler CreateHandler()
    {
        X509Certificate2 root = Load(RootResource);
        if (root == null) return null;
        return new SberHandler(root, Load(SubResource));
    }

    private static X509Certificate2 Load(string resource)
    {
        try
        {
            TextAsset asset = Resources.Load<TextAsset>(resource);
            if (asset == null) return null;
            byte[] data = asset.bytes;
            string text = Encoding.ASCII.GetString(data);
            if (text.Contains("-----BEGIN"))
            {
                var sb = new StringBuilder();
                foreach (string line in text.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("-----", StringComparison.Ordinal)) continue;
                    sb.Append(t);
                }
                data = Convert.FromBase64String(sb.ToString());
            }
            return new X509Certificate2(data);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLM] Не удалось прочитать сертификат " + resource + ": " + ex.Message);
            return null;
        }
    }

    private sealed class SberHandler : CertificateHandler
    {
        private readonly X509Certificate2 _root;
        private readonly X509Certificate2 _sub;

        public SberHandler(X509Certificate2 root, X509Certificate2 sub) { _root = root; _sub = sub; }

        protected override bool ValidateCertificate(byte[] certificateData)
        {
            try
            {
                var leaf = new X509Certificate2(certificateData);
                string name = leaf.GetNameInfo(X509NameType.DnsName, false) ?? string.Empty;
                if (!name.EndsWith("sberbank.ru", StringComparison.OrdinalIgnoreCase)) return false;
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    chain.ChainPolicy.ExtraStore.Add(_root);
                    if (_sub != null) chain.ChainPolicy.ExtraStore.Add(_sub);
                    if (!chain.Build(leaf) || chain.ChainElements.Count == 0) return false;
                    X509Certificate2 top = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                    return string.Equals(top.Thumbprint, _root.Thumbprint, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LLM] Проверка сертификата Сбера не пройдена: " + ex.Message);
                return false;
            }
        }
    }
}
