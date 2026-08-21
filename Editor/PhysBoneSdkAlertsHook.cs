#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
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
            if (EditorApplication.timeSinceStartup - _lastCheckTime < 1.0) return;
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
            if (panel == null || panel == _lastPanel && _bannerContainer != null) return;

            _lastPanel = panel;

            try
            {
                if (panel.rootVisualElement != null)
                {
                    if (_bannerContainer != null && panel.rootVisualElement.Contains(_bannerContainer))
                    {
                        panel.rootVisualElement.Remove(_bannerContainer);
                    }

                    _bannerContainer = new IMGUIContainer(DrawSdkAlertBanner);
                    _bannerContainer.style.marginBottom = 4;
                    _bannerContainer.style.marginTop = 4;

                    panel.rootVisualElement.Insert(0, _bannerContainer);
                }
            }
            catch { }
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

            // Render Alert Banner in VRChat SDK Control Panel
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.12f, 0.65f, 0.95f) }
            };
            GUILayout.Label($"⚡ [VRC PhysBone Merger {PhysBonePackageInfo.Version}]", titleStyle);
            GUILayout.Label($"({avatarRoot.name})", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(PhysBoneLocalization.Tr("定位组件", "Select"), EditorStyles.miniButton, GUILayout.Width(70)))
            {
                Selection.activeGameObject = avatarRoot;
                EditorGUIUtility.PingObject(activeMerger);
            }
            GUILayout.EndHorizontal();

            string desc = PhysBoneLocalization.Tr(
                $"【动骨自动合并预估】：当前 PhysBone 组件数: {stats.CurrentBoneCount} ({stats.CurrentRank})\n" +
                $"➜ 上传构建时将自动合并为: {stats.PredictedBoneCount} 个组件 ({stats.PredictedRank})\n" +
                $"✨ 预计成功合并 {stats.MergedGroupCount} 组，消减 {stats.ReducedBoneCount} 个 PhysBone！(策略: {activeMerger.Strategy})",
                $"[Auto Merge Estimation]: Current PhysBones: {stats.CurrentBoneCount} ({stats.CurrentRank})\n" +
                $"➜ Will be compressed on build to: {stats.PredictedBoneCount} ({stats.PredictedRank})\n" +
                $"✨ Successfully reducing {stats.ReducedBoneCount} PhysBone components! (Strategy: {activeMerger.Strategy})");

            MessageType msgType = stats.ReducedBoneCount > 0 ? MessageType.Info : MessageType.None;
            EditorGUILayout.HelpBox(desc, msgType);

            EditorGUILayout.EndVertical();
        }
    }
}
#endif