using System.Globalization;
using Barabashka.Contracts;
using UnityEngine;

namespace Barabashka.Core
{
    /// <summary>
    /// Настройки такси (PlayerPrefs): оператор по умолчанию и адреса дома и работы.
    /// Координаты адреса кэшируются под текст, с которым найдены: другой текст сбрасывает кэш.
    /// </summary>
    public static class TaxiSettings
    {
        public const string Auto = "auto";
        public const string Home = "home";
        public const string Work = "work";

        private const string OperatorKey = "taxi.operator";

        /// <summary>Идентификатор оператора или «auto» (первый из установленных на телефоне).</summary>
        public static string Operator
        {
            get { return PlayerPrefs.GetString(OperatorKey, Auto); }
            set { PlayerPrefs.SetString(OperatorKey, string.IsNullOrEmpty(value) ? Auto : value); PlayerPrefs.Save(); }
        }

        public static string SlotTitle(string slot)
        {
            return slot == Work ? "Работа" : "Дом";
        }

        public static string GetAddress(string slot)
        {
            return PlayerPrefs.GetString(Key(slot, "text"), string.Empty);
        }

        public static void SetAddress(string slot, string address)
        {
            string text = (address ?? string.Empty).Trim();
            PlayerPrefs.SetString(Key(slot, "text"), text);
            PlayerPrefs.DeleteKey(Key(slot, "lat"));
            PlayerPrefs.DeleteKey(Key(slot, "lon"));
            PlayerPrefs.DeleteKey(Key(slot, "title"));
            PlayerPrefs.Save();
        }

        /// <summary>Найденные координаты сохранённого адреса или null, если ещё не искали.</summary>
        public static TaxiPlace GetCached(string slot)
        {
            double lat, lon;
            if (!double.TryParse(PlayerPrefs.GetString(Key(slot, "lat"), string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out lat) ||
                !double.TryParse(PlayerPrefs.GetString(Key(slot, "lon"), string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                return null;
            return new TaxiPlace { Lat = lat, Lon = lon, Title = PlayerPrefs.GetString(Key(slot, "title"), GetAddress(slot)) };
        }

        public static void SetCached(string slot, TaxiPlace place)
        {
            if (place == null) return;
            PlayerPrefs.SetString(Key(slot, "lat"), place.Lat.ToString("R", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key(slot, "lon"), place.Lon.ToString("R", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key(slot, "title"), place.Title ?? string.Empty);
            PlayerPrefs.Save();
        }

        private static string Key(string slot, string part)
        {
            return "taxi." + (slot == Work ? Work : Home) + "." + part;
        }
    }
}
