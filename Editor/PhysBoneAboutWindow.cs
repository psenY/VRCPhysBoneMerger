#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    public class PhysBoneAboutWindow : EditorWindow
    {
        [MenuItem("Tools/VRC PhysBone Merger", false, 100)]
        public static void Open()
        {
            var win = GetWindow<PhysBoneAboutWindow>(true, "VRC PhysBone Merger", true);
            win.minSize = new Vector2(460, 360);
            win.maxSize = new Vector2(460, 360);
            win.Show();
        }

        public static void AddComponentToSelected()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog(
                    PhysBoneLocalization.Tr("提示", "Notice"),
                    PhysBoneLocalization.Tr("请先在 Hierarchy 层级面板中选中您的 Avatar 模型根对象！", "Please select your Avatar root GameObject in the Hierarchy first!"),
                    "OK");
                return;
            }

            var existing = go.GetComponent<PhysBoneAutoMerger>();
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog(
                    PhysBoneLocalization.Tr("提示", "Notice"),
                    PhysBoneLocalization.Tr($"该对象 [{go.name}] 上已存在 PhysBone Auto Merger 组件！", $"Component already exists on [{go.name}]!"),
                    "OK");
                return;
            }

            Undo.AddComponent<PhysBoneAutoMerger>(go);
            EditorUtility.DisplayDialog(
                PhysBoneLocalization.Tr("成功", "Success"),
                PhysBoneLocalization.Tr($"已成功为 [{go.name}] 添加【PhysBone Auto Merger (动骨自动合并组件)】！\n\n现在可直接正常点击 VRChat 上传或进入 Play 测试，插件将在极晚期自动完成无损合并。",
                    $"Successfully added [PhysBone Auto Merger] to [{go.name}]!\n\nYou can now upload to VRChat or enter Play mode normally."),
                "OK");
        }

        private void OnGUI()
        {
            PhysBoneLocalization.DrawLanguageSelector();
            EditorGUILayout.Space(8);

            // Title Box
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("⚡ VRC PhysBone Merger", EditorStyles.boldLabel);
            GUILayout.Label(PhysBoneLocalization.Tr("专为 VRChat 设计的高性能、非破坏性动骨合并与优化工具", "High-performance non-destructive VRChat PhysBone merging & optimization tool"), EditorStyles.miniLabel);
            GUILayout.Label(PhysBoneLocalization.Tr($"版本: {PhysBonePackageInfo.Version}  |  作者: psenY7  |  协议: GPL-3.0", $"Version: {PhysBonePackageInfo.Version}  |  Author: psenY7  |  License: GPL-3.0"), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Workflow Guide
            EditorGUILayout.HelpBox(
                PhysBoneLocalization.Tr(
                    "📖 纯非破坏性工作流 (Non-Destructive Workflow)：\n\n" +
                    "1. 选中场景中的 Avatar 根节点，添加 【PhysBone Auto Merger】 组件。\n" +
                    "2. 在 Inspector 面板选择适合的预设等级 (UltraStrict / Strict / Balanced / Aggressive / Custom)。\n" +
                    "3. 正常点击 VRChat 上传或进入 Play 模式，插件将在内存克隆体中全自动完成合并与优化，原模型 100% 保持完好！",
                    "📖 Non-Destructive Workflow Guide:\n\n" +
                    "1. Select your Avatar root in Hierarchy and add the [PhysBone Auto Merger] component.\n" +
                    "2. Choose a preset level in Inspector (UltraStrict / Strict / Balanced / Aggressive / Custom).\n" +
                    "3. Build & Publish to VRChat or enter Play Mode. Merging happens automatically in memory clone with zero damage to source prefabs!"),
                MessageType.Info);

            EditorGUILayout.Space(8);

            // 1-Click Action Button
            if (GUILayout.Button(PhysBoneLocalization.Tr("⚡ 为当前选中的模型添加合并组件 (Add to Selected)", "⚡ Add Auto Merger to Selected Avatar"), GUILayout.Height(30)))
            {
                AddComponentToSelected();
            }

            EditorGUILayout.Space(6);

            // Hierarchy Badges Toggle
            bool showBadges = PhysBoneHierarchyOverlay.IsEnabled;
            bool newShowBadges = EditorGUILayout.ToggleLeft(
                PhysBoneLocalization.Tr(" 🏷️ 在 Hierarchy 层级窗口显示动骨数量徽章", " 🏷️ Show PhysBone Count Badges in Hierarchy"),
                showBadges);
            if (newShowBadges != showBadges)
            {
                PhysBoneHierarchyOverlay.IsEnabled = newShowBadges;
            }

            EditorGUILayout.Space(6);

            // External Links
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(PhysBoneLocalization.Tr("🌐 GitHub 开源仓库", "🌐 GitHub Repository"), GUILayout.Height(26)))
            {
                Application.OpenURL("https://github.com/psenY/VRCPhysBoneMerger");
            }
            if (GUILayout.Button(PhysBoneLocalization.Tr("📦 VCC 订阅总仓库", "📦 VPM / VCC Repository"), GUILayout.Height(26)))
            {
                Application.OpenURL("https://psenY.github.io/vpm-repository/");
            }
            GUILayout.EndHorizontal();
        }
    }
}
#endif