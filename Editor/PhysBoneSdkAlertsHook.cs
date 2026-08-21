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
        private static VisualElement _alertRowElement = null;
        private static Label _alertTextLabel = null;
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
            if (EditorApplication.timeSinceStartup - _lastCheckTime < 0.4) return;
            _lastCheckTime = EditorApplication.timeSinceStartup;

            TryAttachBanner();
            RefreshAlertText();
        }

        private static void TryAttachBanner()
        {
            var windows = Resources.FindObjectsOfTypeAll<VRCSdkControlPanel>();
            if (windows == null || windows.Length == 0)
            {
                _lastPanel = null;
                _alertRowElement = null;
                return;
            }

            var panel = windows[0];
            if (panel == null || panel.rootVisualElement == null) return;

            VisualElement targetContainer = FindReviewAlertsContainer(panel.rootVisualElement);
            if (targetContainer == null) return;

            if (_alertRowElement != null && _alertRowElement.parent != targetContainer)
            {
                _alertRowElement.RemoveFromHierarchy();
                _alertRowElement = null;
            }

            if (_alertRowElement == null)
            {
                _alertRowElement = CreateNativeAlertRow();
                if (targetContainer.childCount > 0)
                {
                    targetContainer.Insert(0, _alertRowElement);
                }
                else
                {
                    targetContainer.Add(_alertRowElement);
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

        private static VisualElement CreateNativeAlertRow()
        {
            // Outer Row Container matching native SDK alert row layout
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 44;
            row.style.marginTop = 2;
            row.style.marginBottom = 3;
            row.style.marginLeft = 0;
            row.style.marginRight = 0;
            row.style.alignItems = Align.Stretch;

            // Left Text & Icon Box (Matching #2e2e2e background, 1px border, 3px border-radius)
            var leftBox = new VisualElement();
            leftBox.style.flexDirection = FlexDirection.Row;
            leftBox.style.flexGrow = 1;
            leftBox.style.alignItems = Align.Center;
            leftBox.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            leftBox.style.borderTopWidth = 1;
            leftBox.style.borderBottomWidth = 1;
            leftBox.style.borderLeftWidth = 1;
            leftBox.style.borderRightWidth = 1;
            leftBox.style.borderTopColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            leftBox.style.borderBottomColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            leftBox.style.borderLeftColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            leftBox.style.borderRightColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            leftBox.style.borderTopLeftRadius = 3;
            leftBox.style.borderTopRightRadius = 3;
            leftBox.style.borderBottomLeftRadius = 3;
            leftBox.style.borderBottomRightRadius = 3;
            leftBox.style.paddingLeft = 8;
            leftBox.style.paddingRight = 8;
            leftBox.style.paddingTop = 4;
            leftBox.style.paddingBottom = 4;
            leftBox.style.marginRight = 4;

            // Flat Badge Icon (Blue/Cyan Circle with Zap / Info)
            var iconBox = new VisualElement();
            iconBox.style.width = 28;
            iconBox.style.height = 28;
            iconBox.style.minWidth = 28;
            iconBox.style.minHeight = 28;
            iconBox.style.borderTopLeftRadius = 14;
            iconBox.style.borderTopRightRadius = 14;
            iconBox.style.borderBottomLeftRadius = 14;
            iconBox.style.borderBottomRightRadius = 14;
            iconBox.style.backgroundColor = new Color(0.08f, 0.55f, 0.90f, 1f);
            iconBox.style.justifyContent = Justify.Center;
            iconBox.style.alignItems = Align.Center;
            iconBox.style.marginRight = 8;

            var iconLabel = new Label("⚡");
            iconLabel.style.color = Color.white;
            iconLabel.style.fontSize = 15;
            iconLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            iconBox.Add(iconLabel);
            leftBox.Add(iconBox);

            // Message Label
            _alertTextLabel = new Label("PhysBone Auto Merger: Scanning...");
            _alertTextLabel.style.flexGrow = 1;
            _alertTextLabel.style.whiteSpace = WhiteSpace.Normal;
            _alertTextLabel.style.fontSize = 11;
            _alertTextLabel.style.color = new Color(0.88f, 0.88f, 0.88f, 1f);
            _alertTextLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            leftBox.Add(_alertTextLabel);

            row.Add(leftBox);

            // Right Select Button (Matching native SDK "Select" button styling & layout)
            var selectBtn = new Button(OnSelectButtonClicked);
            selectBtn.text = "Select";
            selectBtn.style.width = 75;
            selectBtn.style.minWidth = 75;
            selectBtn.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            selectBtn.style.borderTopWidth = 1;
            selectBtn.style.borderBottomWidth = 1;
            selectBtn.style.borderLeftWidth = 1;
            selectBtn.style.borderRightWidth = 1;
            selectBtn.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            selectBtn.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            selectBtn.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            selectBtn.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            selectBtn.style.borderTopLeftRadius = 3;
            selectBtn.style.borderTopRightRadius = 3;
            selectBtn.style.borderBottomLeftRadius = 3;
            selectBtn.style.borderBottomRightRadius = 3;
            selectBtn.style.fontSize = 12;
            selectBtn.style.color = Color.white;
            selectBtn.style.alignSelf = Align.Stretch;

            row.Add(selectBtn);

            return row;
        }

        private static void RefreshAlertText()
        {
            if (_alertTextLabel == null) return;

            var autoMergers = UnityEngine.Object.FindObjectsOfType<PhysBoneAutoMerger>();
            if (autoMergers == null || autoMergers.Length == 0)
            {
                if (_alertRowElement != null) _alertRowElement.style.display = DisplayStyle.None;
                return;
            }

            PhysBoneAutoMerger activeMerger = null;
            for (int i = 0; i < autoMergers.Length; i++)
            {
                if (autoMergers[i] != null && autoMergers[i].gameObject.activeInHierarchy && autoMergers[i].EnabledOnUpload)
                {
                    activeMerger = autoMergers[i];
                    break;
                }
            }

            if (activeMerger == null)
            {
                if (_alertRowElement != null) _alertRowElement.style.display = DisplayStyle.None;
                return;
            }

            if (_alertRowElement != null) _alertRowElement.style.display = DisplayStyle.Flex;

            GameObject avatarRoot = activeMerger.gameObject;
            var clusters = PhysBoneMergeCore.Scan(avatarRoot, activeMerger);
            var stats = PhysBoneMergeCore.Evaluate(avatarRoot, clusters);

            if (PhysBoneLocalization.IsChinese)
            {
                _alertTextLabel.text = $"PhysBone 动骨合并组件: 当前包含 {stats.CurrentBoneCount} 个动骨，上传时将自动合并为 {stats.PredictedBoneCount} 个组件 (消减 {stats.ReducedBoneCount} 个，策略: {activeMerger.Strategy})。";
            }
            else
            {
                _alertTextLabel.text = $"PhysBone Auto Merger: Avatar has {stats.CurrentBoneCount} PhysBones, will auto-merge to {stats.PredictedBoneCount} on upload (reducing {stats.ReducedBoneCount} components, Strategy: {activeMerger.Strategy}).";
            }
        }

        private static void OnSelectButtonClicked()
        {
            var autoMergers = UnityEngine.Object.FindObjectsOfType<PhysBoneAutoMerger>();
            if (autoMergers == null || autoMergers.Length == 0) return;

            for (int i = 0; i < autoMergers.Length; i++)
            {
                if (autoMergers[i] != null && autoMergers[i].gameObject.activeInHierarchy)
                {
                    Selection.activeGameObject = autoMergers[i].gameObject;
                    EditorGUIUtility.PingObject(autoMergers[i]);
                    return;
                }
            }
        }
    }
}
#endif