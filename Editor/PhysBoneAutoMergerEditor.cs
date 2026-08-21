#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    [CustomEditor(typeof(PhysBoneAutoMerger))]
    [CanEditMultipleObjects]
    public class PhysBoneAutoMergerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PhysBoneAutoMerger targetScript = (PhysBoneAutoMerger)target;

            if (PhysBoneLocalization.DrawLanguageSelector())
            {
                Repaint();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                PhysBoneLocalization.Tr(
                    $"🛡️ 非破坏性动骨合并 ({PhysBonePackageInfo.Version}) 已就绪\n\n" +
                    "无需生成备份，也不修改源 Prefab！点击 VRChat 上传或进入 Play 测试时，将在内存临时克隆体中自动合并并优化 PhysBone，原模型 100% 保持完好。",
                    $"🛡️ Non-Destructive PhysBone Merger ({PhysBonePackageInfo.Version}) Ready\n\n" +
                    "No manual backups or prefab edits needed! When building/uploading to VRChat or entering Play Mode, PhysBones will be automatically merged on temporary in-memory clone."),
                MessageType.Info);

            EditorGUILayout.Space(6);
            serializedObject.Update();

            var enabledProp = serializedObject.FindProperty("EnabledOnUpload");
            if (enabledProp != null)
            {
                enabledProp.boolValue = EditorGUILayout.ToggleLeft(
                    PhysBoneLocalization.Tr(" [✓] 上传 / 运行测试时自动合并动骨", " [✓] Auto Merge on Upload / Play Mode"),
                    enabledProp.boolValue,
                    EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(4);

            var strategyProp = serializedObject.FindProperty("Strategy");
            if (strategyProp != null)
            {
                string[] labels = new string[]
                {
                    PhysBoneLocalization.Tr("UltraStrict 极限无损", "UltraStrict"),
                    PhysBoneLocalization.Tr("Strict 严格零风险", "Strict (Zero Risk)"),
                    PhysBoneLocalization.Tr("Balanced 平衡优化", "Balanced"),
                    PhysBoneLocalization.Tr("Aggressive 激进压缩", "Aggressive"),
                    PhysBoneLocalization.Tr("Custom 自定义", "Custom")
                };

                strategyProp.enumValueIndex = EditorGUILayout.Popup(
                    PhysBoneLocalization.Tr("合并策略", "Merge Strategy"),
                    strategyProp.enumValueIndex,
                    labels);

                if (strategyProp.enumValueIndex == (int)PhysBoneAutoMerger.MergeStrategy.Custom)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("NumericTolerance"),
                        new GUIContent(PhysBoneLocalization.Tr("数值容差", "Numeric Tolerance")));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("CurveTolerance"),
                        new GUIContent(PhysBoneLocalization.Tr("曲线容差", "Curve Tolerance")));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("IgnoreLimitsRotation"),
                        new GUIContent(PhysBoneLocalization.Tr("忽略限位旋转", "Ignore Limits Rotation")));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("IgnoreCurves"),
                        new GUIContent(PhysBoneLocalization.Tr("忽略曲线差异", "Ignore Curves")));
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("DeduplicateColliders"),
                new GUIContent(PhysBoneLocalization.Tr("自动去重碰撞体", "Deduplicate Colliders")));

            serializedObject.ApplyModifiedProperties();

            // Real-time Performance Prediction Dashboard
            if (targetScript != null && targetScript.gameObject != null)
            {
                EditorGUILayout.Space(8);
                try
                {
                    var clusters = PhysBoneMergeCore.Scan(targetScript.gameObject, targetScript);
                    var stats = PhysBoneMergeCore.Evaluate(targetScript.gameObject, clusters);

                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Tr("📊 实时构建预测与性能评级", "📊 Real-time Build & Performance Preview"),
                        EditorStyles.boldLabel);
                    EditorGUILayout.Space(2);

                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Tr(
                            $"• 动骨组件：{stats.CurrentBoneCount} ➜ 预测压缩至 {stats.PredictedBoneCount} 个 (减少 {stats.ReducedBoneCount} 个)",
                            $"• PhysBone Count: {stats.CurrentBoneCount} ➜ Predicted {stats.PredictedBoneCount} (Reduced {stats.ReducedBoneCount})"));

                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Tr(
                            $"• 合并组数：即将自动合并 {stats.MergedGroupCount} 组同层级近似动骨",
                            $"• Merge Groups: {stats.MergedGroupCount} compatible clusters found"));

                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Tr(
                            $"• 性能等级：{stats.CurrentRank} ➜ {stats.PredictedRank}",
                            $"• Performance Rank: {stats.CurrentRank} ➜ {stats.PredictedRank}"),
                        EditorStyles.boldLabel);

                    EditorGUILayout.EndVertical();
                }
                catch { }
            }
        }
    }
}
#endif