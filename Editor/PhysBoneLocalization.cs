#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    internal enum PhysBoneLanguage
    {
        Chinese = 0,
        English = 1
    }

    internal static class PhysBoneLocalization
    {
        const string LANGUAGE_PREF_KEY = "PsenY7.VRCPhysBoneMerger.Language";

        public static PhysBoneLanguage CurrentLanguage
        {
            get => (PhysBoneLanguage)EditorPrefs.GetInt(LANGUAGE_PREF_KEY, (int)PhysBoneLanguage.Chinese);
            set => EditorPrefs.SetInt(LANGUAGE_PREF_KEY, (int)value);
        }

        public static bool IsChinese => CurrentLanguage == PhysBoneLanguage.Chinese;

        public static string Text(string chinese, string english)
        {
            return IsChinese ? chinese : english;
        }

        public static GUIContent Content(string chinese, string english, string chineseTooltip = null, string englishTooltip = null)
        {
            return new GUIContent(Text(chinese, english), Text(chineseTooltip, englishTooltip));
        }

        public static bool DrawLanguageToggle()
        {
            PhysBoneLanguage current = CurrentLanguage;
            PhysBoneLanguage next;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(Text("语言", "Language"), EditorStyles.miniLabel, GUILayout.Width(60));
                next = (PhysBoneLanguage)GUILayout.Toolbar(
                    (int)current,
                    new[] { "中文", "English" },
                    GUILayout.Width(130));
            }

            if (next != current)
            {
                CurrentLanguage = next;
                GUI.FocusControl(null);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                return true;
            }

            return false;
        }

        [MenuItem("Tools/VRC PhysBone Merger/Language/切换为中文 (Switch to Chinese)", false, 500)]
        [MenuItem("模型工具/VRC 动骨合并器/语言/切换为中文", false, 500)]
        public static void SwitchToChinese()
        {
            CurrentLanguage = PhysBoneLanguage.Chinese;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem("Tools/VRC PhysBone Merger/Language/Switch to English", false, 501)]
        [MenuItem("模型工具/VRC 动骨合并器/语言/Switch to English", false, 501)]
        public static void SwitchToEnglish()
        {
            CurrentLanguage = PhysBoneLanguage.English;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
#endif
