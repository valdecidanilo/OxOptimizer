using UnityEditor;

namespace OxenteGames.OxOptimizer
{
    public enum OxLanguage
    {
        English = 0,
        PortugueseBrazil = 1,
    }

    /// <summary>
    /// Minimal localization for the OxOptimizer UI. English is the default; the choice is
    /// persisted per-machine in EditorPrefs. Strings are declared inline at the call site
    /// with <see cref="T"/> so there is no key table to keep in sync.
    /// </summary>
    public static class OxLoc
    {
        private const string PrefsKey = "OxOptimizer.Language";

        private static OxLanguage? _language;

        public static OxLanguage Language
        {
            get
            {
                if (_language == null)
                    _language = (OxLanguage)EditorPrefs.GetInt(PrefsKey, (int)OxLanguage.English);
                return _language.Value;
            }
            set
            {
                _language = value;
                EditorPrefs.SetInt(PrefsKey, (int)value);
            }
        }

        /// <summary>Returns the English or Brazilian Portuguese text based on the selected language.</summary>
        public static string T(string english, string portuguese)
        {
            return Language == OxLanguage.PortugueseBrazil ? portuguese : english;
        }
    }
}
