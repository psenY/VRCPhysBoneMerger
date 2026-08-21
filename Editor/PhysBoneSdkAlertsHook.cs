#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase.Editor;

namespace PsenY7.VRCPhysBoneMerger
{
    [InitializeOnLoad]
    public static class PhysBoneSdkAlertsHook
    {
        private static EditorWindow _lastPanel = null;
        private static IMGUIContainer _bannerContainer = null;
        private static double _lastCheckTime = 0;

        static PhysBoneSdkAlertsHook()
        {
            VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnabled;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnSdkPanelEnabled(object sender, EventArgs e)
        {
            EditorApplication.delayCall += TryAttachBanner;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastCheckTime < 0.5) return;
            _lastCheckTime = EditorApplication.timeSinceStartup;

            TryAttachBanner();
        }

        private static void TryAttachBanner()
        {
            var windows = Resources.FindObjectsOfTypeAll<VRCSdkControlPanel>();
            if (windows == null || windows.Length == 0)
            {
                _lastPanel = null;
                _bannerContainer = null;
                return;
            }

            var panel = windows[0];
            if (panel == null || panel.rootVisualElement == null) return;

            VisualElement targetContainer = FindReviewAlertsContainer(panel.rootVisualElement);
            if (targetContainer == null)
            {
                targetContainer = panel.rootVisualElement;
            }

            if (_bannerContainer != null && _bannerContainer.parent != targetContainer)
            {
                _bannerContainer.RemoveFromHierarchy();
                _bannerContainer = null;
            }

            if (_bannerContainer == null)
            {
                _bannerContainer = new IMGUIContainer(DrawSdkAlertBanner);
                _bannerContainer.style.marginTop = 2;
                _bannerContainer.style.marginBottom = 2;
                _bannerContainer.style.marginLeft = 4;
                _bannerContainer.style.marginRight = 4;

                if (targetContainer != null)
                {
                    targetContainer.Insert(0, _bannerContainer);
                }
            }

            _lastPanel = panel;
        }

        private static VisualElement FindReviewAlertsContainer(VisualElement root)
        {
            if (root == null) return null;

            var allElements = root.Query<VisualElement>().ToList();
            VisualElement alertsHeader = null;

            for (int i = 0; i < allElements.Count; i++)
            {
                var el = allElements[i];

                if (el is Foldout fo && !string.IsNullOrEmpty(fo.text) && fo.text.Contains("Review Any Alerts"))
                {
                    return fo.contentContainer ?? fo;
                }

                if (el is TextElement te && !string.IsNullOrEmpty(te.text) && te.text.Contains("Review Any Alerts"))
                {
                    alertsHeader = el;
                    break;
                }

                if (el is Label lbl && !string.IsNullOrEmpty(lbl.text) && lbl.text.Contains("Review Any Alerts"))
                {
                    alertsHeader = el;
                    break;
                }
            }

            if (alertsHeader != null)
            {
                var parent = alertsHeader.parent;
                while (parent != null)
                {
                    var container = parent.Q<ScrollView>() ?? parent.Q(className: "unity-foldout__content");
                    if (container != null) return container;

                    if (parent.childCount > 1 && parent != root) return parent;
                    parent = parent.parent;
                }
            }

            return null;
        }

        private static void DrawSdkAlertBanner()
        {
            var autoMergers = UnityEngine.Object.FindObjectsOfType<PhysBoneAutoMerger>();
            if (autoMergers == null || autoMergers.Length == 0) return;

            PhysBoneAutoMerger activeMerger = null;
            for (int i = 0; i < autoMergers.Length; i++)
            {
                if (autoMergers[i] != null && autoMergers[i].gameObject.activeInHierarchy && autoMergers[i].EnabledOnUpload)
                {
                    activeMerger = autoMergers[i];
                    break;
                }
            }

            if (activeMerger == null) return;

            GameObject avatarRoot = activeMerger.gameObject;
            var clusters = PhysBoneMergeCore.Scan(avatarRoot, activeMerger);
            var stats = PhysBoneMergeCore.Evaluate(avatarRoot, clusters);

            if (stats.CurrentBoneCount == 0) return;

            // Render Native-styled Alert Entry matching VRChat SDK "Review Any Alerts" rows
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(0, 0, 2, 2)
            };

            EditorGUILayout.BeginHorizontal(boxStyle);

            // Left Column: Info Icon
            GUIContent iconContent = EditorGUIUtility.IconContent("console.infoicon");
            GUILayout.Label(iconContent, GUILayout.Width(28), GUILayout.Height(36));

            // Middle Column: Alert Details
            EditorGUILayout.BeginVertical();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.22f, 0.74f, 0.97f) }
            };
            GUILayout.Label($"⚡ VRC PhysBone Merger ({PhysBonePackageInfo.Version}) - 动骨自动优化", titleStyle);

            GUIStyle textStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                fontSize = 10,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            string desc = PhysBoneLocalization.Tr(
                $"动骨组件数: {stats.CurrentBoneCount} ➜ 上传构建时将自动合并为: {stats.PredictedBoneCount} ({stats.PredictedRank})\n" +
                $"预计成功消减 {stats.ReducedBoneCount} 个 PhysBone 组件 (策略: {activeMerger.Strategy})，非破坏性生效。",
                $"PhysBone Components: {stats.CurrentBoneCount} ➜ Will be merged on upload to: {stats.PredictedBoneCount} ({stats.PredictedRank})\n" +
                $"Successfully reducing {stats.ReducedBoneCount} components (Strategy: {activeMerger.Strategy}).");
            GUILayout.Label(desc, textStyle);
            EditorGUILayout.EndVertical();

            // Right Column: Native "Select" Button
            if (GUILayout.Button(PhysBoneLocalization.Tr("Select", "Select"), GUILayout.Width(75), GUILayout.Height(36)))
            {
                Selection.activeGameObject = avatarRoot;
                EditorGUIUtility.PingObject(activeMerger);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif