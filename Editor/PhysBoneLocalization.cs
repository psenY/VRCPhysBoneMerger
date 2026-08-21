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
}
#endif