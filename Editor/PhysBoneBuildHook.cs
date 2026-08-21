#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace PsenY7.VRCPhysBoneMerger
{
    public class PhysBoneBuilderApiCallback : IVRCSDKPreprocessAvatarCallback
    {
        // Run extremely late, after NDMF, Modular Avatar, VRCFury, and Triturbo FaceTracking
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

        public static void ProcessAvatar(GameObject avatarGameObject)
        {
            if (avatarGameObject == null) return;

            try
            {
                var autoMergers = avatarGameObject.GetComponentsInChildren<PhysBoneAutoMerger>(true);
                if (autoMergers == null || autoMergers.Length == 0) return;

                for (int i = 0; i < autoMergers.Length; i++)
                {
                    var autoMerger = autoMergers[i];
                    if (autoMerger != null && autoMerger.EnabledOnUpload)
                    {
                        var options = autoMerger.GetOptions();
                        var scanResult = PhysBoneMergeUtility.ScanHierarchy(avatarGameObject, options, true);
                        if (scanResult != null && scanResult.CandidateGroups != null && scanResult.CandidateGroups.Count > 0)
                        {
                            List<PhysBoneMergeUtility.PhysBoneMergeResult> results;
                            PhysBoneMergeUtility.TryMergeGroups(scanResult.CandidateGroups, out results, false);
                            Debug.Log($"[VRC PhysBone Merger] 非破坏性自动合并完成：成功自动合并了 {results.Count} 组 PhysBone 组件！原始项目 Prefab 完好无损。");
                        }
                    }
                    
                    if (autoMerger != null)
                    {
                        UnityEngine.Object.DestroyImmediate(autoMerger);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VRC PhysBone Merger] Auto merge process notice: {ex.Message}");
            }
        }
    }
}
#endif
