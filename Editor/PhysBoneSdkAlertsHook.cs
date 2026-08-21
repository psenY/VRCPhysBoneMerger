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
        private static Texture2D _infoBadgeTexture = null;
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
            if (EditorApplication.timeSinceStartup - _lastCheckTime < 0.3) return;
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
                _alertRowElement = CreateUnifiedAlertBanner();
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

        private static VisualElement CreateUnifiedAlertBanner()
        {
            // Unified Full-width Alert Banner
            var banner = new VisualElement();
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.minHeight = 42;
            banner.style.marginTop = 2;
            banner.style.marginBottom = 4;
            banner.style.marginLeft = 0;
            banner.style.marginRight = 0;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            banner.style.borderTopWidth = 1;
            banner.style.borderBottomWidth = 1;
            banner.style.borderLeftWidth = 1;
            banner.style.borderRightWidth = 1;
            banner.style.borderTopColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            banner.style.borderBottomColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            banner.style.borderLeftColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            banner.style.borderRightColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            banner.style.borderTopLeftRadius = 3;
            banner.style.borderTopRightRadius = 3;
            banner.style.borderBottomLeftRadius = 3;
            banner.style.borderBottomRightRadius = 3;
            banner.style.paddingLeft = 10;
            banner.style.paddingRight = 10;
            banner.style.paddingTop = 6;
            banner.style.paddingBottom = 6;

            // Click banner to select avatar in Hierarchy
            banner.RegisterCallback<ClickEvent>(evt => OnSelectButtonClicked());

            // Left Icon Badge
            if (_infoBadgeTexture == null)
            {
                _infoBadgeTexture = CreateBlueInfoBadge();
            }

            var iconImage = new Image();
            iconImage.image = _infoBadgeTexture;
            iconImage.style.width = 28;
            iconImage.style.height = 28;
            iconImage.style.minWidth = 28;
            iconImage.style.minHeight = 28;
            iconImage.style.marginRight = 10;
            iconImage.style.flexShrink = 0;
            banner.Add(iconImage);

            // Message Label
            _alertTextLabel = new Label("PhysBone Auto Merger: Scanning...");
            _alertTextLabel.style.flexGrow = 1;
            _alertTextLabel.style.flexShrink = 1;
            _alertTextLabel.style.whiteSpace = WhiteSpace.Normal;
            _alertTextLabel.style.fontSize = 11;
            _alertTextLabel.style.color = new Color(0.88f, 0.88f, 0.88f, 1f);
            _alertTextLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            banner.Add(_alertTextLabel);

            return banner;
        }

        private static Texture2D CreateBlueInfoBadge()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color transparent = new Color(0, 0, 0, 0);
            Color blue = new Color(0.08f, 0.58f, 0.92f, 1f);
            Color white = Color.white;

            float center = size / 2f;
            float radius = size * 0.44f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                        tex.SetPixel(x, y, new Color(blue.r, blue.g, blue.b, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }

            // Draw 'i' icon
            int dotY = (int)(size * 0.69f);
            int barTopY = (int)(size * 0.53f);
            int barBottomY = (int)(size * 0.27f);
            int barWidth = 4;
            int centerX = size / 2;

            for (int y = dotY - 2; y <= dotY + 2; y++)
            {
                for (int x = centerX - 2; x <= centerX + 2; x++)
                {
                    tex.SetPixel(x, y, white);
                }
            }
            for (int y = barBottomY; y <= barTopY; y++)
            {
                for (int x = centerX - barWidth / 2; x < centerX + barWidth / 2; x++)
                {
                    tex.SetPixel(x, y, white);
                }
            }

            tex.Apply();
            return tex;
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
                _alertTextLabel.text = $"PhysBone 动骨自动合并: 模型当前包含 {stats.CurrentBoneCount} 个动骨。上传构建时将自动合并为 {stats.PredictedBoneCount} 个组件 (消减 {stats.ReducedBoneCount} 个，策略: {activeMerger.Strategy})。";
            }
            else
            {
                _alertTextLabel.text = $"PhysBone Auto Merger: Avatar has {stats.CurrentBoneCount} PhysBones. Will auto-merge to {stats.PredictedBoneCount} on upload (reducing {stats.ReducedBoneCount} components, Strategy: {activeMerger.Strategy}).";
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