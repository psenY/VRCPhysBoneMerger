#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    public class PhysBoneMergeWindow : EditorWindow
    {
        const string StrategyExportExtension = "physboneStrategy.json";

        GameObject _rootObject;
        Vector2 _scrollPosition;
        GUIContent _refreshIcon;
        PhysBoneMergeUtility.HierarchyScanResult _scanResult;
        List<PhysBoneMergeStrategyStore.StrategyDefinition> _strategies = new List<PhysBoneMergeStrategyStore.StrategyDefinition>();
        string _selectedStrategyId = PhysBoneMergeStrategyStore.StrictStrategyId;
        PhysBoneMergeUtility.ApproximationOptions _currentOptions;
        string _saveStrategyName = string.Empty;

        Dictionary<PhysBoneMergeUtility.PhysBoneSiblingGroup, bool> _groupSelections = new Dictionary<PhysBoneMergeUtility.PhysBoneSiblingGroup, bool>();
        Dictionary<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone, bool> _boneSelections = new Dictionary<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone, bool>();

        bool _nonDestructiveMode = true;

        [MenuItem("Tools/VRC 动骨合并器 (PhysBone Merger)", false, 100)]
        [MenuItem("模型工具/VRC 动骨合并器 (PhysBone Merger)", false, 100)]
        static void Init()
        {
            PhysBoneMergeWindow window = (PhysBoneMergeWindow)GetWindow(typeof(PhysBoneMergeWindow));
            window.titleContent = new GUIContent(PhysBoneLocalization.Text("合并近似动骨", "Merge Similar PhysBones"));
            window.Show();
        }

        [MenuItem("GameObject/模型工具/VRC 动骨合并器 (PhysBone Merger)", true, 0)]
        static bool CanShowFromSelection() => Selection.activeGameObject != null;

        [MenuItem("GameObject/模型工具/VRC 动骨合并器 (PhysBone Merger)", false, 0)]
        static void ShowFromSelection() => OpenForAvatar(Selection.activeGameObject);

        public static void OpenForAvatar(GameObject avatar)
        {
            PhysBoneMergeWindow window = (PhysBoneMergeWindow)GetWindow(typeof(PhysBoneMergeWindow));
            window.titleContent = new GUIContent(PhysBoneLocalization.Text("合并近似动骨", "Merge Similar PhysBones"));
            if (avatar != null) window._rootObject = avatar;
            window.Show();
        }

        void OnEnable()
        {
            _refreshIcon = EditorGUIUtility.IconContent("RotateTool On", "Rescan");
            ReloadStrategies(true);
        }

        void OnGUI()
        {
            if (PhysBoneLocalization.DrawLanguageToggle())
            {
                titleContent = new GUIContent(PhysBoneLocalization.Text("合并近似动骨", "Merge Similar PhysBones"));
                ReloadStrategies(true);
                Repaint();
            }

            if (_currentOptions == null)
            {
                ReloadStrategies(true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"<size=20><color=magenta>{PhysBoneLocalization.Text("psenY7 的近似动骨合并器", "psenY7's Similar PhysBone Merger")}</color></size> v2.0",
                new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawInputSection();
            DrawPerformancePreviewSection();
            DrawStrategySection();
            DrawOptionsSection();
            DrawScanControls();
            DrawResultsSection();
            EditorGUILayout.EndScrollView();
        }

        void DrawPerformancePreviewSection()
        {
            if (_rootObject == null) return;

            var stats = PhysBoneMergeUtility.CalculatePerformanceStats(_rootObject);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text("⚡ 动骨优化效益与 VRChat Performance Rating 预览", "⚡ PhysBone Optimization & Performance Rating Preview"),
                EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            int totalSelectedBones = 0;
            int selectedGroupsCount = 0;
            if (_scanResult != null && _scanResult.CandidateGroups != null)
            {
                for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
                {
                    var group = _scanResult.CandidateGroups[i];
                    if (group == null || group.Bones == null) continue;
                    if (_groupSelections.TryGetValue(group, out bool gSel) && !gSel) continue;

                    int count = 0;
                    for (int j = 0; j < group.Bones.Count; j++)
                    {
                        if (group.Bones[j] != null && _boneSelections.TryGetValue(group.Bones[j], out bool bSel) && bSel)
                            count++;
                    }

                    if (count >= 2)
                    {
                        selectedGroupsCount++;
                        totalSelectedBones += count;
                    }
                }
            }

            int predictedCompCount = stats.ComponentCount - totalSelectedBones + selectedGroupsCount;
            if (predictedCompCount < 0) predictedCompCount = 0;
            string predictedRating = PhysBoneMergeUtility.GetPhysBoneRank(predictedCompCount, stats.TransformCount, stats.ColliderCount);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(PhysBoneLocalization.Text("当前动骨组件数：", "Current Components:"), GUILayout.Width(130));
                EditorGUILayout.LabelField($"{stats.ComponentCount} 个", EditorStyles.boldLabel, GUILayout.Width(80));

                EditorGUILayout.LabelField(PhysBoneLocalization.Text("合并后预估：", "Predicted After Merge:"), GUILayout.Width(120));
                string compReductionText = selectedGroupsCount > 0 ? $"{predictedCompCount} 个 (减少 {totalSelectedBones - selectedGroupsCount} 个组件)" : $"{stats.ComponentCount} 个";
                EditorGUILayout.LabelField(compReductionText, EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(PhysBoneLocalization.Text("VRChat Rating：", "VRChat Rank:"), GUILayout.Width(130));
                EditorGUILayout.LabelField(GetRankColoredLabel(stats.Rating), new GUIStyle(EditorStyles.boldLabel) { richText = true }, GUILayout.Width(120));

                EditorGUILayout.LabelField(PhysBoneLocalization.Text("预估 Rating：", "Predicted Rank:"), GUILayout.Width(100));
                EditorGUILayout.LabelField(GetRankColoredLabel(predictedRating), new GUIStyle(EditorStyles.boldLabel) { richText = true });
            }

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(PhysBoneLocalization.Text("包含碰撞体数：", "Colliders:"), GUILayout.Width(130));
                EditorGUILayout.LabelField($"{stats.ColliderCount} 个", GUILayout.Width(80));

                if (GUILayout.Button(PhysBoneLocalization.Text("🧹 一键清理整机 Missing/重复碰撞体", "🧹 Cleanup Missing/Duplicate Colliders")))
                {
                    int cleaned = PhysBoneMergeUtility.CleanupPhysBoneColliders(_rootObject, true);
                    EditorUtility.DisplayDialog(
                        PhysBoneLocalization.Text("碰撞体清理完成", "Collider Cleanup Complete"),
                        PhysBoneLocalization.Text($"成功清理并去重了 {cleaned} 个 PhysBone 的碰撞体列表！", $"Successfully cleaned up and deduplicated colliders on {cleaned} PhysBone components!"),
                        PhysBoneLocalization.Text("确定", "OK"));
                    ScanNow();
                }
            }

            EditorGUILayout.EndVertical();
        }

        static string GetRankColoredLabel(string rank)
        {
            switch (rank)
            {
                case "Excellent": return "<color=green>Excellent (极佳)</color>";
                case "Good": return "<color=#7FFF00>Good (良好)</color>";
                case "Medium": return "<color=yellow>Medium (中等)</color>";
                case "Poor": return "<color=orange>Poor (较差)</color>";
                default: return "<color=red>Very Poor (极差)</color>";
            }
        }

        void DrawInputSection()
        {
            EditorGUILayout.LabelField(PhysBoneLocalization.Text("输入与无损模式设置", "Input & Non-Destructive Settings"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _rootObject != null;
                if (GUILayout.Button(_refreshIcon, GUILayout.Width(30), GUILayout.Height(30)))
                    ScanNow();
                GUI.enabled = true;

                _rootObject = (GameObject)EditorGUILayout.ObjectField(
                    GUIContent.none,
                    _rootObject,
                    typeof(GameObject),
                    true,
                    GUILayout.Height(30));
            }

            if (EditorGUI.EndChangeCheck())
                _scanResult = null;

            EditorGUILayout.Space(4);
            _nonDestructiveMode = EditorGUILayout.ToggleLeft(
                PhysBoneLocalization.Text(
                    "🛡️ 非破坏性模式（合并时自动生成副本模型，原模型/Prefab 100% 完好无损）",
                    "🛡️ Non-Destructive Mode (Creates optimized clone, source prefab untouched)"),
                _nonDestructiveMode,
                EditorStyles.boldLabel);

            if (_rootObject != null)
            {
                bool hasAutoMerger = _rootObject.GetComponent<PhysBoneAutoMerger>() != null;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        hasAutoMerger
                            ? PhysBoneLocalization.Text("✨ 已挂载非破坏性自动上传组件（VRCFury/MA 风格）", "✨ Non-destructive Auto Merger component attached")
                            : PhysBoneLocalization.Text("💡 推荐：挂载 VRCFury/MA 风格零破坏上传组件", "💡 Recommend: Attach VRCFury/MA style non-destructive merger"),
                        EditorStyles.miniLabel);

                    if (!hasAutoMerger && GUILayout.Button(PhysBoneLocalization.Text("一键添加组件", "Add Component"), GUILayout.Width(100)))
                    {
                        Undo.AddComponent<PhysBoneAutoMerger>(_rootObject);
                        EditorUtility.DisplayDialog(
                            PhysBoneLocalization.Text("成功添加", "Successfully Added"),
                            PhysBoneLocalization.Text("已成功为模型挂载【VRC 动骨非破坏性自动合并组件】！\n\n以后只需点击 VRChat 控制台上传，系统就会在临时的上传内存副本中自动合并动骨。您工程中的原模型和 Prefab 保持 100% 0 破坏状态。", "Non-Destructive Auto Merger attached! PhysBones will automatically merge during VRChat upload in temporary memory, leaving your source project avatar 100% untouched."),
                            PhysBoneLocalization.Text("确定", "OK"));
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        void DrawStrategySection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(PhysBoneLocalization.Text("策略", "Strategy"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            int selectedIndex = GetSelectedStrategyIndex();
            string[] labels = new string[_strategies.Count];
            for (int i = 0; i < _strategies.Count; i++)
            {
                labels[i] = _strategies[i].GetDisplayName();
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(PhysBoneLocalization.Text("当前策略", "Current Strategy"), selectedIndex, labels);
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < _strategies.Count)
            {
                ApplyStrategy(_strategies[nextIndex]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _saveStrategyName = EditorGUILayout.TextField(
                    PhysBoneLocalization.Text("保存名称", "Save Name"),
                    string.IsNullOrEmpty(_saveStrategyName) ? GetSuggestedStrategyName() : _saveStrategyName);

                if (GUILayout.Button(PhysBoneLocalization.Text("保存为策略", "Save As Strategy"), GUILayout.Width(120)))
                {
                    SaveCurrentAsStrategy();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(PhysBoneLocalization.Text("导入策略", "Import Strategy")))
                {
                    ImportStrategy();
                }

                if (GUILayout.Button(PhysBoneLocalization.Text("导出策略", "Export Strategy")))
                {
                    ExportSelectedStrategy();
                }

                GUI.enabled = CanDeleteSelectedStrategy();
                if (GUILayout.Button(PhysBoneLocalization.Text("删除策略", "Delete Strategy")))
                {
                    DeleteSelectedStrategy();
                }
                GUI.enabled = true;
            }

            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            if (selected != null && selected.Id == PhysBoneMergeStrategyStore.StrictStrategyId)
            {
                EditorGUILayout.HelpBox(
                    PhysBoneLocalization.Text(
                        "严格安全策略会零容差比较所有关键字段、曲线、碰撞体列表及 Parameter 参数名，确保合并后 100% 零物理或功能偏差。",
                        "The strict safe strategy compares all fields, curves, colliders, and parameter names with zero tolerance, ensuring 100% zero physics or feature drift."),
                    MessageType.Info);
            }
            else if (selected != null && selected.Id == PhysBoneMergeStrategyStore.AggressiveStrategyId)
            {
                EditorGUILayout.HelpBox(
                    PhysBoneLocalization.Text(
                        "激进策略会尽量保留旧版的宽松合并逻辑，命中更多，但 Play 模式下更容易出现物理表现偏差。",
                        "The aggressive strategy keeps the old loose matching behavior as much as possible. It finds more candidates, but is more likely to drift in Play mode."),
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawOptionsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(PhysBoneLocalization.Text("判定设置", "Detection Settings"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            using (new EditorGUI.DisabledScope(IsSelectedStrategyReadOnly()))
            {
                _currentOptions.NumericTolerance = EditorGUILayout.Slider(
                    PhysBoneLocalization.Text("数值容差", "Numeric Tolerance"),
                    _currentOptions.NumericTolerance,
                    0.001f,
                    1f);
                _currentOptions.CurveTolerance = EditorGUILayout.Slider(
                    PhysBoneLocalization.Text("曲线容差", "Curve Tolerance"),
                    _currentOptions.CurveTolerance,
                    0.001f,
                    1f);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(PhysBoneLocalization.Text("高级忽略项", "Advanced Ignore Options"), EditorStyles.boldLabel);
                _currentOptions.IgnoreLimitsRotation = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("旋转限制", "limitRotation"),
                    _currentOptions.IgnoreLimitsRotation);
                _currentOptions.IgnoreEndpointPosition = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("端点位置", "endpointPosition"),
                    _currentOptions.IgnoreEndpointPosition);
                _currentOptions.IgnoreCurves = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("参数曲线", "curves"),
                    _currentOptions.IgnoreCurves);
                _currentOptions.IgnoreTypeModes = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("类型模式", "integrationType / immobileType / limitType"),
                    _currentOptions.IgnoreTypeModes);
                _currentOptions.IgnoreMultiChildType = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("多子节点处理", "multiChildType"),
                    _currentOptions.IgnoreMultiChildType);
                _currentOptions.IgnoreIgnoreTransforms = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("排除列表", "ignoreTransforms"),
                    _currentOptions.IgnoreIgnoreTransforms);
                _currentOptions.IgnoreEndpointStructure = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("端点模式差异", "endpoint structure"),
                    _currentOptions.IgnoreEndpointStructure);
                _currentOptions.IgnoreChildStructure = EditorGUILayout.ToggleLeft(
                    IgnoreLabel("子链结构差异", "child structure"),
                    _currentOptions.IgnoreChildStructure);
            }

            EditorGUILayout.HelpBox(
                PhysBoneLocalization.Text(
                    "严格安全策略会零容差比较关键字段、曲线、碰撞体和结构，推荐首选。自定义策略可自由调整容差和忽略项。",
                    "The strict safe strategy compares key fields, curves, colliders, and structures strictly. Custom strategy lets you tweak tolerances."),
                MessageType.Info);
            EditorGUILayout.EndVertical();

            PersistCustomOptionsIfNeeded();
        }

        void DrawScanControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(PhysBoneLocalization.Text("扫描", "Scan"), EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.enabled = _rootObject != null;
            if (GUILayout.Button(PhysBoneLocalization.Text("扫描可合并动骨", "Scan Merge Candidates"), GUILayout.Height(28)))
                ScanNow();
            GUI.enabled = true;

            if (_rootObject == null)
            {
                EditorGUILayout.HelpBox(
                    PhysBoneLocalization.Text("请先拖入一个根对象。", "Please assign a root object first."),
                    MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        void DrawResultsSection()
        {
            if (_scanResult == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(PhysBoneLocalization.Text("结果", "Results"), EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"扫描了 {_scanResult.ParentCountScanned} 个父节点，命中 {_scanResult.CandidateGroupCount} 组候选，共 {_scanResult.CandidatePhysBoneCount} 个 VRC PhysBone 组件。",
                    $"Scanned {_scanResult.ParentCountScanned} parent nodes and found {_scanResult.CandidateGroupCount} candidate groups covering {_scanResult.CandidatePhysBoneCount} VRC PhysBone components."));
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"实际扫到的 VRC PhysBone 总数：{_scanResult.PhysBoneCountScanned}",
                    $"Total VRC PhysBones found: {_scanResult.PhysBoneCountScanned}"));
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"含动骨的父节点数：{_scanResult.ParentCountWithPhysBones}",
                    $"Parent nodes containing PhysBones: {_scanResult.ParentCountWithPhysBones}"));
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"因 rootTransform 不兼容被排除：{_scanResult.PhysBoneCountExcludedByRootTransform}",
                    $"Excluded by incompatible rootTransform: {_scanResult.PhysBoneCountExcludedByRootTransform}"));
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"因同层级不足 2 个被排除：{_scanResult.PhysBoneCountExcludedByMissingSibling}",
                    $"Excluded because the same parent has fewer than 2: {_scanResult.PhysBoneCountExcludedByMissingSibling}"));
            EditorGUILayout.LabelField(
                PhysBoneLocalization.Text(
                    $"通过层级与 rootTransform 但未通过当前策略：{_scanResult.PhysBoneCountExcludedByApproximation}",
                    $"Passed hierarchy and rootTransform checks but failed the current strategy: {_scanResult.PhysBoneCountExcludedByApproximation}"));

            if (_scanResult.ApproximationFailureExamples != null && _scanResult.ApproximationFailureExamples.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    PhysBoneLocalization.Text("⚠️ 排除合并原因及差异示例（未并入原因分析）", "⚠️ Excluded Mismatches & Failure Examples"),
                    EditorStyles.boldLabel);

                for (int i = 0; i < _scanResult.ApproximationFailureExamples.Count; i++)
                {
                    var example = _scanResult.ApproximationFailureExamples[i];
                    if (example == null)
                        continue;

                    string leftPath = PhysBoneMergeUtility.GetRelativePath(_rootObject != null ? _rootObject.transform : null, example.Left != null ? example.Left.transform : null);
                    string rightPath = PhysBoneMergeUtility.GetRelativePath(_rootObject != null ? _rootObject.transform : null, example.Right != null ? example.Right.transform : null);
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(
                            PhysBoneLocalization.Text(
                                $"⚠️ 关键差异拒并字段 [{example.MismatchField}]\n• 骨骼 A: {leftPath}\n• 骨骼 B: {rightPath}",
                                $"⚠️ Rejected Mismatch Field [{example.MismatchField}]\n• Bone A: {leftPath}\n• Bone B: {rightPath}"),
                            MessageType.Warning);

                        if (GUILayout.Button(PhysBoneLocalization.Text("对比定位", "Ping Both"), GUILayout.Width(70), GUILayout.Height(38)))
                        {
                            if (example.Left != null && example.Right != null)
                            {
                                Selection.objects = new UnityEngine.Object[] { example.Left.gameObject, example.Right.gameObject };
                                EditorGUIUtility.PingObject(example.Left.gameObject);
                            }
                        }
                    }
                }
            }

            if (_scanResult.CandidateGroupCount == 0)
            {
                EditorGUILayout.HelpBox(
                    PhysBoneLocalization.Text(
                        "当前没有找到可合并的兄弟动骨组。默认严格安全策略会零误差比较；如果你确认这批动骨本来就很接近，可以尝试切换到自定义/激进策略，或逐项放宽。",
                        "No mergeable sibling PhysBone groups were found. The default strict safe strategy compares with zero error; if you know these PhysBones are very close, you can switch strategies or relax options."),
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(PhysBoneLocalization.Text("全选", "Select All"), GUILayout.Width(80)))
                    {
                        SetAllSelections(true);
                    }
                    if (GUILayout.Button(PhysBoneLocalization.Text("全不选", "Deselect All"), GUILayout.Width(80)))
                    {
                        SetAllSelections(false);
                    }

                    int selGroups = GetSelectedGroupCount();
                    int selBones = GetSelectedBoneCount();
                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Text(
                            $"已勾选 {selGroups} 组候选 (共 {selBones} 个组件)",
                            $"Selected {selGroups} groups ({selBones} components)"),
                        EditorStyles.boldLabel);
                }
                EditorGUILayout.Space();

                for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
                    DrawCandidateGroup(i, _scanResult.CandidateGroups[i]);

                EditorGUILayout.Space();
                if (GUILayout.Button(PhysBoneLocalization.Text("执行合并已勾选项", "Run Merge Selected"), GUILayout.Height(30)))
                    RunMerge();
            }

            EditorGUILayout.EndVertical();
        }

        void SetAllSelections(bool select)
        {
            if (_scanResult == null || _scanResult.CandidateGroups == null) return;
            for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
            {
                var group = _scanResult.CandidateGroups[i];
                if (group == null) continue;
                _groupSelections[group] = select;
                if (group.Bones != null)
                {
                    for (int j = 0; j < group.Bones.Count; j++)
                    {
                        if (group.Bones[j] != null)
                            _boneSelections[group.Bones[j]] = select;
                    }
                }
            }
        }

        int GetSelectedGroupCount()
        {
            if (_scanResult == null || _scanResult.CandidateGroups == null) return 0;
            int count = 0;
            for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
            {
                var group = _scanResult.CandidateGroups[i];
                if (group == null || group.Bones == null) continue;
                if (_groupSelections.TryGetValue(group, out bool gSel) && gSel)
                {
                    int checkedBones = 0;
                    for (int j = 0; j < group.Bones.Count; j++)
                    {
                        if (group.Bones[j] != null && _boneSelections.TryGetValue(group.Bones[j], out bool bSel) && bSel)
                            checkedBones++;
                    }
                    if (checkedBones >= 2) count++;
                }
            }
            return count;
        }

        int GetSelectedBoneCount()
        {
            if (_scanResult == null || _scanResult.CandidateGroups == null) return 0;
            int count = 0;
            for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
            {
                var group = _scanResult.CandidateGroups[i];
                if (group == null || group.Bones == null) continue;
                if (_groupSelections.TryGetValue(group, out bool gSel) && gSel)
                {
                    int checkedInGroup = 0;
                    for (int j = 0; j < group.Bones.Count; j++)
                    {
                        if (group.Bones[j] != null && _boneSelections.TryGetValue(group.Bones[j], out bool bSel) && bSel)
                            checkedInGroup++;
                    }
                    if (checkedInGroup >= 2) count += checkedInGroup;
                }
            }
            return count;
        }

        void PingCandidateGroup(PhysBoneMergeUtility.PhysBoneSiblingGroup group)
        {
            if (group == null || group.Bones == null || group.Bones.Count == 0) return;

            List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            for (int i = 0; i < group.Bones.Count; i++)
            {
                if (group.Bones[i] != null && group.Bones[i].gameObject != null)
                {
                    objects.Add(group.Bones[i].gameObject);
                }
            }

            if (objects.Count > 0)
            {
                Selection.objects = objects.ToArray();
                EditorGUIUtility.PingObject(objects[0]);
            }
        }

        void DrawCandidateGroup(int index, PhysBoneMergeUtility.PhysBoneSiblingGroup group)
        {
            if (group == null || group.Bones == null) return;

            bool isGroupSelected = _groupSelections.TryGetValue(group, out bool gSel) ? gSel : true;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                isGroupSelected = EditorGUILayout.ToggleLeft(
                    PhysBoneLocalization.Text(
                        $"候选组 {index + 1}  |  父物体：{PhysBoneMergeUtility.GetRelativePath(_rootObject != null ? _rootObject.transform : null, group.Parent)}",
                        $"Candidate {index + 1}  |  Parent: {PhysBoneMergeUtility.GetRelativePath(_rootObject != null ? _rootObject.transform : null, group.Parent)}"),
                    isGroupSelected,
                    EditorStyles.boldLabel);

                if (EditorGUI.EndChangeCheck())
                {
                    _groupSelections[group] = isGroupSelected;
                    for (int i = 0; i < group.Bones.Count; i++)
                    {
                        if (group.Bones[i] != null)
                            _boneSelections[group.Bones[i]] = isGroupSelected;
                    }
                }

                if (GUILayout.Button(PhysBoneLocalization.Text("定位整组", "Ping Group"), GUILayout.Width(70)))
                {
                    PingCandidateGroup(group);
                }
            }

            int checkedCount = 0;
            List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone> checkedBones = new List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
            for (int i = 0; i < group.Bones.Count; i++)
            {
                if (group.Bones[i] != null && _boneSelections.TryGetValue(group.Bones[i], out bool bSel) && bSel)
                {
                    checkedCount++;
                    checkedBones.Add(group.Bones[i]);
                }
            }

            string smartName = PhysBoneMergeUtility.GetSmartMergedName(checkedBones.Count > 0 ? checkedBones : group.Bones);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    PhysBoneLocalization.Text(
                        $"组件数量：{group.Bones.Count} (已勾选: {checkedCount})",
                        $"Component Count: {group.Bones.Count} (Selected: {checkedCount})"));

                EditorGUILayout.LabelField(
                    PhysBoneLocalization.Text($"预计生成：{smartName}", $"Smart Name: {smartName}"),
                    EditorStyles.miniLabel);
            }

            if (!string.IsNullOrEmpty(group.FirstMismatchField))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        PhysBoneLocalization.Text(
                            $"💡 同级邻近骨骼排除说明：组外同级骨骼因 [{group.FirstMismatchField}] 不一致而未被纳入本组（确保了本组合并安全）。\n• 对比参考：{group.FirstMismatchSummary}",
                            $"💡 Excluded Sibling Mismatch: Nearby bone excluded due to [{group.FirstMismatchField}] mismatch.\n• Compared: {group.FirstMismatchSummary}"),
                        MessageType.Info);

                    if (group.FirstMismatchReference != null && group.FirstMismatchCandidate != null)
                    {
                        if (GUILayout.Button(PhysBoneLocalization.Text("对比定位", "Ping Both"), GUILayout.Width(70), GUILayout.Height(35)))
                        {
                            Selection.objects = new UnityEngine.Object[] { group.FirstMismatchReference.gameObject, group.FirstMismatchCandidate.gameObject };
                            EditorGUIUtility.PingObject(group.FirstMismatchReference.gameObject);
                        }
                    }
                }
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < group.Bones.Count; i++)
            {
                var physBone = group.Bones[i];
                if (physBone == null)
                    continue;

                bool isBoneSelected = _boneSelections.TryGetValue(physBone, out bool bSel) ? bSel : true;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    isBoneSelected = EditorGUILayout.ToggleLeft(
                        PhysBoneMergeUtility.GetRelativePath(_rootObject != null ? _rootObject.transform : null, physBone.transform),
                        isBoneSelected);

                    if (EditorGUI.EndChangeCheck())
                    {
                        _boneSelections[physBone] = isBoneSelected;
                        int count = 0;
                        for (int j = 0; j < group.Bones.Count; j++)
                        {
                            if (group.Bones[j] != null && _boneSelections.TryGetValue(group.Bones[j], out bool sel) && sel)
                                count++;
                        }
                        _groupSelections[group] = count > 0;
                    }

                    if (GUILayout.Button(PhysBoneLocalization.Text("定位", "Ping"), GUILayout.Width(45)))
                    {
                        EditorGUIUtility.PingObject(physBone.gameObject);
                        Selection.activeGameObject = physBone.gameObject;
                    }
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        void ScanNow()
        {
            if (_rootObject == null)
                return;

            PersistCustomOptionsIfNeeded();
            _scanResult = PhysBoneMergeUtility.ScanHierarchy(_rootObject, PhysBoneMergeUtility.CloneOptions(_currentOptions), true);

            _groupSelections.Clear();
            _boneSelections.Clear();
            if (_scanResult != null && _scanResult.CandidateGroups != null)
            {
                for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
                {
                    var group = _scanResult.CandidateGroups[i];
                    if (group == null) continue;
                    _groupSelections[group] = true;
                    if (group.Bones != null)
                    {
                        for (int j = 0; j < group.Bones.Count; j++)
                        {
                            if (group.Bones[j] != null)
                                _boneSelections[group.Bones[j]] = true;
                        }
                    }
                }
            }

            Repaint();
        }

        void RunMerge()
        {
            if (_rootObject == null || _scanResult == null || _scanResult.CandidateGroupCount == 0)
                return;

            List<PhysBoneMergeUtility.PhysBoneSiblingGroup> selectedGroupsToMerge = new List<PhysBoneMergeUtility.PhysBoneSiblingGroup>();
            int totalSelectedBoneCount = 0;

            for (int i = 0; i < _scanResult.CandidateGroups.Count; i++)
            {
                var group = _scanResult.CandidateGroups[i];
                if (group == null || group.Bones == null) continue;

                if (_groupSelections.TryGetValue(group, out bool gSel) && !gSel)
                    continue;

                List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone> checkedBones = new List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
                for (int j = 0; j < group.Bones.Count; j++)
                {
                    var bone = group.Bones[j];
                    if (bone != null && _boneSelections.TryGetValue(bone, out bool bSel) && bSel)
                    {
                        checkedBones.Add(bone);
                    }
                }

                if (checkedBones.Count >= 2)
                {
                    var customSubGroup = new PhysBoneMergeUtility.PhysBoneSiblingGroup
                    {
                        Parent = group.Parent,
                        Bones = checkedBones,
                        MatchOptions = group.MatchOptions,
                        FirstMismatchField = group.FirstMismatchField,
                        FirstMismatchSummary = group.FirstMismatchSummary,
                        FirstMismatchReference = group.FirstMismatchReference,
                        FirstMismatchCandidate = group.FirstMismatchCandidate
                    };
                    selectedGroupsToMerge.Add(customSubGroup);
                    totalSelectedBoneCount += checkedBones.Count;
                }
            }

            if (selectedGroupsToMerge.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    PhysBoneLocalization.Text("提示", "Notice"),
                    PhysBoneLocalization.Text("请先勾选至少一组（且包含至少 2 个动骨组件）要合并的项。", "Please select at least one candidate group (with at least 2 components) to merge."),
                    PhysBoneLocalization.Text("确定", "OK"));
                return;
            }

            string confirmMsg = _nonDestructiveMode
                ? $"即将按“{GetSelectedStrategyLabel()}”处理已勾选的 {selectedGroupsToMerge.Count} 组候选 (共 {totalSelectedBoneCount} 个组件)。\n\n【非破坏性模式】将自动保留并隐藏原始模型，并创建一个全新的“{_rootObject.name} (PhysBone Optimized)”副本进行合并。工程文件 100% 0 破坏。是否继续？"
                : $"即将按“{GetSelectedStrategyLabel()}”处理已勾选的 {selectedGroupsToMerge.Count} 组候选 (共 {totalSelectedBoneCount} 个组件)。\n\n注意：此模式会直接修改当前选中的模型节点。支持 Undo。是否继续？";

            bool confirmed = EditorUtility.DisplayDialog(
                PhysBoneLocalization.Text("确认合并动骨", "Confirm PhysBone Merge"),
                PhysBoneLocalization.Text(confirmMsg, confirmMsg),
                PhysBoneLocalization.Text("执行", "Run"),
                PhysBoneLocalization.Text("取消", "Cancel"));

            if (!confirmed)
                return;

            if (_nonDestructiveMode)
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("PhysBone Non-Destructive Merge");

                GameObject avatarCopy = Instantiate(_rootObject);
                avatarCopy.name = _rootObject.name + " (PhysBone Optimized)";
                Undo.RegisterCreatedObjectUndo(avatarCopy, "PhysBone Non-Destructive Merge");

                Undo.RecordObject(_rootObject, "Deactivate Source Avatar");
                _rootObject.SetActive(false);

                // Map user's checked candidate selection onto avatarCopy using relative paths
                List<PhysBoneMergeUtility.PhysBoneSiblingGroup> cloneGroupsToMerge = new List<PhysBoneMergeUtility.PhysBoneSiblingGroup>();
                Transform sourceRootTransform = _rootObject.transform;
                Transform copyRootTransform = avatarCopy.transform;

                for (int i = 0; i < selectedGroupsToMerge.Count; i++)
                {
                    var srcGroup = selectedGroupsToMerge[i];
                    if (srcGroup == null || srcGroup.Bones == null) continue;

                    string parentRelPath = PhysBoneMergeUtility.GetRelativePath(sourceRootTransform, srcGroup.Parent);
                    Transform copyParent = string.IsNullOrEmpty(parentRelPath) ? copyRootTransform : copyRootTransform.Find(parentRelPath);
                    if (copyParent == null) continue;

                    List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone> copyBones = new List<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
                    for (int j = 0; j < srcGroup.Bones.Count; j++)
                    {
                        var srcBone = srcGroup.Bones[j];
                        if (srcBone == null) continue;

                        string boneRelPath = PhysBoneMergeUtility.GetRelativePath(sourceRootTransform, srcBone.transform);
                        Transform copyBoneTransform = string.IsNullOrEmpty(boneRelPath) ? copyRootTransform : copyRootTransform.Find(boneRelPath);
                        if (copyBoneTransform != null)
                        {
                            var copyBoneComponent = copyBoneTransform.GetComponent<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>();
                            if (copyBoneComponent != null)
                            {
                                copyBones.Add(copyBoneComponent);
                            }
                        }
                    }

                    if (copyBones.Count >= 2)
                    {
                        cloneGroupsToMerge.Add(new PhysBoneMergeUtility.PhysBoneSiblingGroup
                        {
                            Parent = copyParent,
                            Bones = copyBones,
                            MatchOptions = srcGroup.MatchOptions
                        });
                    }
                }

                if (cloneGroupsToMerge.Count > 0)
                {
                    List<PhysBoneMergeUtility.PhysBoneMergeResult> copyResults;
                    PhysBoneMergeUtility.TryMergeGroups(cloneGroupsToMerge, out copyResults, true);
                }

                Undo.CollapseUndoOperations(undoGroup);

                _rootObject = avatarCopy;
                Selection.activeGameObject = avatarCopy;
                ScanNow();

                EditorUtility.DisplayDialog(
                    PhysBoneLocalization.Text("非破坏性合并完成", "Non-Destructive Merge Complete"),
                    PhysBoneLocalization.Text(
                        $"已成功创建优化后的副本模型：\n{avatarCopy.name}\n\n原始模型已被安全隐藏并完整保留，原工程文件 100% 0 破坏。",
                        $"Optimized copy successfully created:\n{avatarCopy.name}\n\nSource avatar hidden and 100% preserved with zero destruction."),
                    PhysBoneLocalization.Text("确定", "OK"));
                return;
            }

            List<PhysBoneMergeUtility.PhysBoneMergeResult> results;
            bool allSucceeded = PhysBoneMergeUtility.TryMergeGroups(selectedGroupsToMerge, out results, true);

            int successCount = 0;
            int mergedBoneCount = 0;
            string firstError = null;
            for (int i = 0; i < results.Count; i++)
            {
                PhysBoneMergeUtility.PhysBoneMergeResult result = results[i];
                if (result == null)
                    continue;

                if (result.Success)
                {
                    successCount++;
                    mergedBoneCount += result.RemovedComponents != null ? result.RemovedComponents.Count : 0;
                }
                else if (string.IsNullOrEmpty(firstError))
                {
                    firstError = result.Error;
                }
            }

            ScanNow();

            EditorUtility.DisplayDialog(
                allSucceeded
                    ? PhysBoneLocalization.Text("合并完成", "Merge Complete")
                    : PhysBoneLocalization.Text("合并部分完成", "Merge Partially Complete"),
                allSucceeded
                    ? PhysBoneLocalization.Text(
                        $"已处理 {successCount} 组，合并 {mergedBoneCount} 个 VRC PhysBone。",
                        $"Processed {successCount} groups and merged {mergedBoneCount} VRC PhysBones.")
                    : PhysBoneLocalization.Text(
                        $"成功处理 {successCount} 组，合并 {mergedBoneCount} 个 VRC PhysBone。\n\n首个错误：{firstError}",
                        $"Successfully processed {successCount} groups and merged {mergedBoneCount} VRC PhysBones.\n\nFirst error: {firstError}"),
                PhysBoneLocalization.Text("确定", "OK"));
        }

        void ReloadStrategies(bool preserveSelection)
        {
            string previousId = preserveSelection ? _selectedStrategyId : PhysBoneMergeStrategyStore.CustomStrategyId;
            _strategies = PhysBoneMergeStrategyStore.LoadStrategies();
            PhysBoneMergeStrategyStore.StrategyDefinition selected = PhysBoneMergeStrategyStore.FindStrategy(_strategies, previousId)
                ?? PhysBoneMergeStrategyStore.FindStrategy(_strategies, PhysBoneMergeStrategyStore.CustomStrategyId)
                ?? (_strategies.Count > 0 ? _strategies[0] : null);

            if (selected != null)
            {
                _selectedStrategyId = selected.Id;
                _currentOptions = PhysBoneMergeUtility.CloneOptions(selected.Options);
            }
            else
            {
                _selectedStrategyId = PhysBoneMergeStrategyStore.CustomStrategyId;
                _currentOptions = PhysBoneMergeStrategyStore.CreateDefaultCustomOptions();
            }
        }

        void ApplyStrategy(PhysBoneMergeStrategyStore.StrategyDefinition strategy)
        {
            if (strategy == null)
            {
                return;
            }

            _selectedStrategyId = strategy.Id;
            _currentOptions = PhysBoneMergeUtility.CloneOptions(strategy.Options);
            _scanResult = null;
            Repaint();
        }

        void PersistCustomOptionsIfNeeded()
        {
            if (_currentOptions == null)
            {
                return;
            }

            if (_selectedStrategyId == PhysBoneMergeStrategyStore.CustomStrategyId)
            {
                PhysBoneMergeStrategyStore.SaveCustomStrategyOptions(_currentOptions);
            }
        }

        void SaveCurrentAsStrategy()
        {
            string name = string.IsNullOrWhiteSpace(_saveStrategyName) ? GetSuggestedStrategyName() : _saveStrategyName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            PhysBoneMergeStrategyStore.SaveUserStrategy(name, _currentOptions);
            ReloadStrategies(false);
            PhysBoneMergeStrategyStore.StrategyDefinition strategy = PhysBoneMergeStrategyStore.FindStrategy(_strategies, "user." + name);
            if (strategy != null)
            {
                ApplyStrategy(strategy);
            }
        }

        void ImportStrategy()
        {
            string selectedPath = EditorUtility.OpenFilePanel(
                PhysBoneLocalization.Text("选择策略文件", "Choose Strategy File"),
                Application.dataPath,
                "json");

            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            try
            {
                string importedName = PhysBoneMergeStrategyStore.ImportStrategy(selectedPath);
                ReloadStrategies(false);
                PhysBoneMergeStrategyStore.StrategyDefinition strategy = PhysBoneMergeStrategyStore.FindStrategy(_strategies, "user." + importedName);
                if (strategy != null)
                {
                    ApplyStrategy(strategy);
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    PhysBoneLocalization.Text("导入失败", "Import Failed"),
                    ex.Message,
                    PhysBoneLocalization.Text("确定", "OK"));
            }
        }

        void ExportSelectedStrategy()
        {
            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            if (selected == null)
            {
                return;
            }

            string selectedPath = EditorUtility.SaveFilePanel(
                PhysBoneLocalization.Text("导出策略", "Export Strategy"),
                Application.dataPath,
                selected.Name + "." + StrategyExportExtension,
                "json");

            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            PhysBoneMergeStrategyStore.ExportStrategy(selectedPath, selected.Name, _currentOptions);
        }

        void DeleteSelectedStrategy()
        {
            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            if (selected == null || !selected.IsDeletable)
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                PhysBoneLocalization.Text("确认删除策略", "Confirm Strategy Deletion"),
                PhysBoneLocalization.Text(
                    $"确定要删除策略“{selected.GetDisplayName()}”吗？",
                    $"Delete strategy \"{selected.GetDisplayName()}\"?"),
                PhysBoneLocalization.Text("删除", "Delete"),
                PhysBoneLocalization.Text("取消", "Cancel"));

            if (!confirmed)
            {
                return;
            }

            PhysBoneMergeStrategyStore.DeleteUserStrategy(selected.Name);
            _selectedStrategyId = PhysBoneMergeStrategyStore.CustomStrategyId;
            ReloadStrategies(false);
        }

        int GetSelectedStrategyIndex()
        {
            for (int i = 0; i < _strategies.Count; i++)
            {
                if (_strategies[i] != null && _strategies[i].Id == _selectedStrategyId)
                {
                    return i;
                }
            }

            return 0;
        }

        PhysBoneMergeStrategyStore.StrategyDefinition GetSelectedStrategy()
        {
            return PhysBoneMergeStrategyStore.FindStrategy(_strategies, _selectedStrategyId);
        }

        bool IsSelectedStrategyReadOnly()
        {
            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            return selected != null && !selected.IsEditable;
        }

        bool CanDeleteSelectedStrategy()
        {
            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            return selected != null && selected.IsDeletable;
        }

        string IgnoreLabel(string chineseMeaning, string englishFieldName)
        {
            return PhysBoneLocalization.IsChinese
                ? "忽略" + chineseMeaning + "（" + englishFieldName + "）"
                : "Ignore " + englishFieldName;
        }

        string GetSelectedStrategyLabel()
        {
            PhysBoneMergeStrategyStore.StrategyDefinition selected = GetSelectedStrategy();
            return selected != null ? selected.GetDisplayName() : PhysBoneLocalization.Text("自定义策略", "Custom Strategy");
        }

        string GetSuggestedStrategyName()
        {
            return PhysBoneLocalization.Text("我的合并策略", "My Merge Strategy");
        }
    }
}
#endif

