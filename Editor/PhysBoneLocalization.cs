#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    internal enum Language
    {
        Chinese = 0,
        English = 1
    }

    internal static class PhysBoneLocalization
    {
        private const string PREF_KEY = "PsenY7.VRCPhysBoneMerger.Language";

        public static Language CurrentLanguage
        {
            get => (Language)EditorPrefs.GetInt(PREF_KEY, (int)Language.Chinese);
            set => EditorPrefs.SetInt(PREF_KEY, (int)value);
        }

        public static bool IsChinese => CurrentLanguage == Language.Chinese;

        public static string Tr(string zh, string en) => IsChinese ? zh : en;

        public static bool DrawLanguageSelector()
        {
            Language cur = CurrentLanguage;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(Tr("语言 / Language", "Language"), EditorStyles.miniLabel, GUILayout.Width(90));
                Language next = (Language)GUILayout.Toolbar((int)cur, new[] { "中文", "English" }, GUILayout.Width(130));
                if (next != cur)
                {
                    CurrentLanguage = next;
                    GUI.FocusControl(null);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    return true;
                }
            }
            return false;
        }
    }

    internal static class PhysBonePackageInfo
    {
        private static string _cachedVersion = null;

        public static string Version
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedVersion)) return _cachedVersion;

                try
                {
                    var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PhysBoneAutoMerger).Assembly);
                    if (info != null && !string.IsNullOrEmpty(info.version))
                    {
                        _cachedVersion = "v" + info.version;
                        return _cachedVersion;
                    }
                }
                catch { }

                try
                {
                    string[] candidates = new string[]
                    {
                        "Packages/pseny7.vrc.physbone-merger/package.json",
                        "Assets/pseny7.vrc.physbone-merger/package.json"
                    };

                    foreach (var path in candidates)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            string json = System.IO.File.ReadAllText(path);
                            var match = System.Text.RegularExpressions.Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                            if (match.Success)
                            {
                                _cachedVersion = "v" + match.Groups[1].Value;
                                return _cachedVersion;
                            }
                        }
                    }
                }
                catch { }

                _cachedVersion = "v1.1.0";
                return _cachedVersion;
            }
        }
    }
}
#endif