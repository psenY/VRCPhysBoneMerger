#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace PsenY7.VRCPhysBoneMerger
{
    public class PhysBoneBuilderApiCallback : IVRCSDKPreprocessAvatarCallback
    {
        // Run at the absolute end after NDMF, Modular Avatar, VRCFury, and FaceTracking
        public int callbackOrder => 999999;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            PhysBoneBuildHook.ProcessAvatar(avatarGameObject);
            return true;
        }
    }

    [InitializeOnLoad]
    public static class PhysBoneBuildHook
    {
        static PhysBoneBuildHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var autoMergers = UnityEngine.Object.FindObjectsOfType<PhysBoneAutoMerger>();
                if (autoMergers == null || autoMergers.Length == 0) return;

                for (int i = 0; i < autoMergers.Length; i++)
                {
                    if (autoMergers[i] != null && autoMergers[i].EnabledOnUpload && autoMergers[i].gameObject != null)
                    {
                        ProcessAvatar(autoMergers[i].gameObject);
                    }
                }
            }
        }

        public static void ProcessAvatar(GameObject avatarRoot)
        {
            if (avatarRoot == null) return;

            try
            {
                var autoMergers = avatarRoot.GetComponentsInChildren<PhysBoneAutoMerger>(true);
                if (autoMergers == null || autoMergers.Length == 0) return;

                for (int i = 0; i < autoMergers.Length; i++)
                {
                    var autoMerger = autoMergers[i];
                    if (autoMerger != null && autoMerger.EnabledOnUpload)
                    {
                        var clusters = PhysBoneMergeCore.Scan(avatarRoot, autoMerger);
                        if (clusters != null && clusters.Count > 0)
                        {
                            int mergedGroups = PhysBoneMergeCore.ExecuteMerge(clusters, autoMerger.DeduplicateColliders, false);
                            Debug.Log($"[VRC PhysBone Merger] 非破坏性自动合并完成：成功合并了 {mergedGroups} 组 PhysBone 组件！");
                        }
                    }

                    // Remove component from build clone to keep final bundle pure
                    if (autoMerger != null)
                    {
                        UnityEngine.Object.DestroyImmediate(autoMerger);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRC PhysBone Merger] Build notice: {ex.Message}");
            }
        }
    }
}
#endif