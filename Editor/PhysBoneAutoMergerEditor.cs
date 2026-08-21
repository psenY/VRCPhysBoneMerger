#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    [CustomEditor(typeof(PhysBoneAutoMerger), true)]
    [CanEditMultipleObjects]
    public class PhysBoneAutoMergerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            PhysBoneAutoMerger targetScript = (PhysBoneAutoMerger)target;

            if (PhysBoneLocalization.DrawLanguageToggle())
            {
                Repaint();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                PhysBoneLocalization.Text(
                    "🛡️ VRCFury / Modular Avatar 风格非破坏性构建组件已激活\n\n" +
                    "您无需手动合并，也无需生成任何副本！只要挂载此组件，点击 VRChat 控制台上传时，系统会自动在临时的上传内存副本中合并 PhysBone。\n\n" +
                    "您的源工程模型和 Prefab 保持 100% 0 破坏状态。",
                    "🛡️ VRCFury / Modular Avatar Style Non-Destructive Merger Active\n\n" +
                    "No manual merging or avatar duplication required! When building/uploading via VRChat, PhysBones will automatically merge on the temporary in-memory clone.\n\n" +
                    "Your source project avatar and prefab remain 100% untouched."),
                MessageType.Info);

            EditorGUILayout.Space();
            serializedObject.Update();

            var enabledProp = serializedObject.FindProperty("EnabledOnUpload");
            if (enabledProp != null)
            {
                enabledProp.boolValue = EditorGUILayout.ToggleLeft(
                    PhysBoneLocalization.Text(" [✓] 上传 VRChat 时自动合并动骨", " [✓] Auto Merge PhysBones on VRChat Upload"),
                    enabledProp.boolValue,
                    EditorStyles.boldLabel);
            }

            EditorGUILayout.Space(4);

            var strategyProp = serializedObject.FindProperty("Strategy");
            if (strategyProp != null)
            {
                string[] strategyLabels = new string[]
                {
                    PhysBoneLocalization.Text("严格模式 (零风险推荐)", "Strict (Zero Risk Recommended)"),
                    PhysBoneLocalization.Text("自定义模式", "Custom Mode"),
                    PhysBoneLocalization.Text("激进模式 (同父节点合并)", "Aggressive (Same Parent)")
                };

                strategyProp.enumValueIndex = EditorGUILayout.Popup(
                    PhysBoneLocalization.Text("合并匹配策略", "Merge Strategy"),
                    strategyProp.enumValueIndex,
                    strategyLabels);

                if (strategyProp.enumValueIndex == (int)PhysBoneAutoMerger.StrategyType.Custom)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("NumericTolerance"),
                        PhysBoneLocalization.Content("数值容差", "Numeric Tolerance"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("CurveTolerance"),
                        PhysBoneLocalization.Content("曲线容差", "Curve Tolerance"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("IgnoreLimitsRotation"),
                        PhysBoneLocalization.Content("忽略限制旋转差异", "Ignore Limits Rotation Difference"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("IgnoreEndpointPosition"),
                        PhysBoneLocalization.Content("忽略末端位置差异", "Ignore Endpoint Position Difference"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("IgnoreCurves"),
                        PhysBoneLocalization.Content("忽略曲线差异", "Ignore Curves Difference"));
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (targetScript != null && targetScript.gameObject != null)
            {
                try
                {
                    var scanResult = PhysBoneMergeUtility.ScanHierarchy(targetScript.gameObject, targetScript.GetOptions(), true);
                    int groupCount = scanResult != null && scanResult.CandidateGroups != null ? scanResult.CandidateGroups.Count : 0;
                    int boneCount = scanResult != null ? scanResult.CandidatePhysBoneCount : 0;

                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Text(
                            $"📊 实时上传构建预测：即将自动合并 {groupCount} 组 (共 {boneCount} 个动骨组件)",
                            $"📊 Upload Prediction: {groupCount} groups ({boneCount} PhysBone components) will be merged automatically"),
                        EditorStyles.boldLabel);

                    if (GUILayout.Button(PhysBoneLocalization.Text("🔍 打开交互式动骨合并窗口", "🔍 Open Interactive PhysBone Merger")))
                    {
                        PhysBoneMergeWindow.OpenForAvatar(targetScript.gameObject);
                    }
                    EditorGUILayout.EndVertical();
                }
                catch
                {
                }
            }
        }
    }
}
#endif
