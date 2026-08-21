#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PsenY7.VRCPhysBoneMerger
{
    internal static class PhysBoneMergeStrategyStore
    {
        internal const string StrictStrategyId = "builtin.strict";
        internal const string CustomStrategyId = "builtin.custom";
        internal const string AggressiveStrategyId = "builtin.aggressive";

        const string UserStrategiesPrefKey = "PsenY7.VRCPhysBoneMerger.PhysBoneMerge.UserStrategies";
        const string CustomStrategyPrefKey = "PsenY7.VRCPhysBoneMerger.PhysBoneMerge.CustomStrategy";

        [Serializable]
        internal sealed class StrategyDefinition
        {
            public string Id;
            public string Name;
            public string NameChinese;
            public string NameEnglish;
            public bool IsBuiltIn;
            public bool IsDeletable;
            public bool IsEditable;
            public PhysBoneMergeUtility.ApproximationOptions Options;

            public string GetDisplayName()
            {
                if (PhysBoneLocalization.IsChinese && !string.IsNullOrEmpty(NameChinese))
                {
                    return NameChinese;
                }

                if (!PhysBoneLocalization.IsChinese && !string.IsNullOrEmpty(NameEnglish))
                {
                    return NameEnglish;
                }

                return string.IsNullOrEmpty(Name) ? "Unnamed Strategy" : Name;
            }
        }

        [Serializable]
        sealed class StrategyFilePayload
        {
            public string Name;
            public PhysBoneMergeUtility.ApproximationOptions Options;
        }

        [Serializable]
        sealed class UserStrategyList
        {
            public List<StrategyFilePayload> Items = new List<StrategyFilePayload>();
        }

        internal static List<StrategyDefinition> LoadStrategies()
        {
            List<StrategyDefinition> strategies = new List<StrategyDefinition>
            {
                CreateStrictStrategy(),
                CreateCustomStrategy(),
                CreateAggressiveStrategy()
            };

            UserStrategyList userStrategies = LoadUserStrategies();
            for (int i = 0; i < userStrategies.Items.Count; i++)
            {
                StrategyFilePayload payload = userStrategies.Items[i];
                if (payload == null || string.IsNullOrWhiteSpace(payload.Name))
                {
                    continue;
                }

                strategies.Add(new StrategyDefinition
                {
                    Id = "user." + payload.Name,
                    Name = payload.Name,
                    NameChinese = payload.Name,
                    NameEnglish = payload.Name,
                    IsBuiltIn = false,
                    IsDeletable = true,
                    IsEditable = true,
                    Options = PhysBoneMergeUtility.CloneOptions(payload.Options)
                });
            }

            return strategies;
        }

        internal static PhysBoneMergeUtility.ApproximationOptions LoadCustomStrategyOptions()
        {
            string json = EditorPrefs.GetString(CustomStrategyPrefKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return CreateDefaultCustomOptions();
            }

            try
            {
                StrategyFilePayload payload = JsonUtility.FromJson<StrategyFilePayload>(json);
                if (payload != null && payload.Options != null)
                {
                    return PhysBoneMergeUtility.CloneOptions(payload.Options);
                }
            }
            catch
            {
            }

            return CreateDefaultCustomOptions();
        }

        internal static void SaveCustomStrategyOptions(PhysBoneMergeUtility.ApproximationOptions options)
        {
            StrategyFilePayload payload = new StrategyFilePayload
            {
                Name = "Custom",
                Options = PhysBoneMergeUtility.CloneOptions(options)
            };
            EditorPrefs.SetString(CustomStrategyPrefKey, JsonUtility.ToJson(payload));
        }

        internal static void SaveUserStrategy(string name, PhysBoneMergeUtility.ApproximationOptions options)
        {
            if (string.IsNullOrWhiteSpace(name) || options == null)
            {
                return;
            }

            UserStrategyList list = LoadUserStrategies();
            StrategyFilePayload existing = list.Items.Find(item => item != null && string.Equals(item.Name, name, StringComparison.Ordinal));
            if (existing == null)
            {
                existing = new StrategyFilePayload();
                list.Items.Add(existing);
            }

            existing.Name = name.Trim();
            existing.Options = PhysBoneMergeUtility.CloneOptions(options);
            SaveUserStrategies(list);
        }

        internal static void DeleteUserStrategy(string strategyName)
        {
            if (string.IsNullOrWhiteSpace(strategyName))
            {
                return;
            }

            UserStrategyList list = LoadUserStrategies();
            list.Items.RemoveAll(item => item != null && string.Equals(item.Name, strategyName, StringComparison.Ordinal));
            SaveUserStrategies(list);
        }

        internal static void ExportStrategy(string path, string strategyName, PhysBoneMergeUtility.ApproximationOptions options)
        {
            StrategyFilePayload payload = new StrategyFilePayload
            {
                Name = strategyName,
                Options = PhysBoneMergeUtility.CloneOptions(options)
            };

            File.WriteAllText(path, JsonUtility.ToJson(payload, true));
        }

        internal static string ImportStrategy(string path)
        {
            string json = File.ReadAllText(path);
            StrategyFilePayload payload = JsonUtility.FromJson<StrategyFilePayload>(json);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Name) || payload.Options == null)
            {
                throw new InvalidDataException("Invalid PhysBone merge strategy file.");
            }

            string finalName = payload.Name.Trim();
            HashSet<string> existingNames = new HashSet<string>(StringComparer.Ordinal);
            UserStrategyList list = LoadUserStrategies();
            for (int i = 0; i < list.Items.Count; i++)
            {
                if (list.Items[i] != null && !string.IsNullOrWhiteSpace(list.Items[i].Name))
                {
                    existingNames.Add(list.Items[i].Name);
                }
            }

            if (existingNames.Contains(finalName))
            {
                int suffix = 2;
                string baseName = finalName;
                while (existingNames.Contains(finalName))
                {
                    finalName = baseName + " (" + suffix + ")";
                    suffix++;
                }
            }

            SaveUserStrategy(finalName, payload.Options);
            return finalName;
        }

        internal static StrategyDefinition FindStrategy(List<StrategyDefinition> strategies, string strategyId)
        {
            if (strategies == null)
            {
                return null;
            }

            for (int i = 0; i < strategies.Count; i++)
            {
                StrategyDefinition definition = strategies[i];
                if (definition != null && string.Equals(definition.Id, strategyId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        internal static PhysBoneMergeUtility.ApproximationOptions CreateDefaultCustomOptions()
        {
            return new PhysBoneMergeUtility.ApproximationOptions
            {
                StrategyMode = PhysBoneMergeUtility.ApproximationStrategyMode.Custom,
                NumericTolerance = PhysBoneMergeUtility.DefaultNumericTolerance,
                CurveTolerance = PhysBoneMergeUtility.DefaultCurveTolerance,
                CurveSampleCount = PhysBoneMergeUtility.DefaultCurveSampleCount,
                IgnoreLimitsRotation = false,
                IgnoreEndpointPosition = false,
                IgnoreCurves = false,
                IgnoreTypeModes = false,
                IgnoreMultiChildType = false,
                IgnoreIgnoreTransforms = false,
                IgnoreEndpointStructure = false,
                IgnoreChildStructure = false
            };
        }

        internal static PhysBoneMergeUtility.ApproximationOptions CreateStrictOptions()
        {
            return new PhysBoneMergeUtility.ApproximationOptions
            {
                StrategyMode = PhysBoneMergeUtility.ApproximationStrategyMode.Custom,
                NumericTolerance = 0.001f,
                CurveTolerance = 0.001f,
                CurveSampleCount = PhysBoneMergeUtility.DefaultCurveSampleCount,
                IgnoreLimitsRotation = false,
                IgnoreEndpointPosition = false,
                IgnoreCurves = false,
                IgnoreTypeModes = false,
                IgnoreMultiChildType = false,
                IgnoreIgnoreTransforms = false,
                IgnoreEndpointStructure = false,
                IgnoreChildStructure = false
            };
        }

        internal static PhysBoneMergeUtility.ApproximationOptions CreateAggressiveOptions()
        {
            return new PhysBoneMergeUtility.ApproximationOptions
            {
                StrategyMode = PhysBoneMergeUtility.ApproximationStrategyMode.Aggressive,
                NumericTolerance = PhysBoneMergeUtility.DefaultNumericTolerance,
                CurveTolerance = PhysBoneMergeUtility.DefaultCurveTolerance,
                CurveSampleCount = PhysBoneMergeUtility.DefaultCurveSampleCount,
                IgnoreLimitsRotation = true,
                IgnoreEndpointPosition = true,
                IgnoreCurves = true,
                IgnoreTypeModes = true,
                IgnoreMultiChildType = true,
                IgnoreIgnoreTransforms = true,
                IgnoreEndpointStructure = true,
                IgnoreChildStructure = true
            };
        }

        static StrategyDefinition CreateStrictStrategy()
        {
            return new StrategyDefinition
            {
                Id = StrictStrategyId,
                Name = "Strict",
                NameChinese = "严格安全策略（零风险）",
                NameEnglish = "Strict Safe Strategy (Zero Risk)",
                IsBuiltIn = true,
                IsDeletable = false,
                IsEditable = false,
                Options = CreateStrictOptions()
            };
        }

        static StrategyDefinition CreateCustomStrategy()
        {
            return new StrategyDefinition
            {
                Id = CustomStrategyId,
                Name = "Custom",
                NameChinese = "自定义策略",
                NameEnglish = "Custom Strategy",
                IsBuiltIn = true,
                IsDeletable = false,
                IsEditable = true,
                Options = LoadCustomStrategyOptions()
            };
        }

        static StrategyDefinition CreateAggressiveStrategy()
        {
            return new StrategyDefinition
            {
                Id = AggressiveStrategyId,
                Name = "Aggressive",
                NameChinese = "激进策略",
                NameEnglish = "Aggressive Strategy",
                IsBuiltIn = true,
                IsDeletable = false,
                IsEditable = false,
                Options = CreateAggressiveOptions()
            };
        }

        static UserStrategyList LoadUserStrategies()
        {
            string json = EditorPrefs.GetString(UserStrategiesPrefKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new UserStrategyList();
            }

            try
            {
                UserStrategyList list = JsonUtility.FromJson<UserStrategyList>(json);
                return list ?? new UserStrategyList();
            }
            catch
            {
                return new UserStrategyList();
            }
        }

        static void SaveUserStrategies(UserStrategyList list)
        {
            EditorPrefs.SetString(UserStrategiesPrefKey, JsonUtility.ToJson(list));
        }

        public static PhysBoneMergeUtility.ApproximationOptions GetOptions(this PhysBoneAutoMerger autoMerger)
        {
            if (autoMerger == null) return CreateStrictOptions();
            switch (autoMerger.Strategy)
            {
                case PhysBoneAutoMerger.StrategyType.Strict:
                    return CreateStrictOptions();
                case PhysBoneAutoMerger.StrategyType.Aggressive:
                    return CreateAggressiveOptions();
                default:
                    var opts = CreateDefaultCustomOptions();
                    opts.NumericTolerance = autoMerger.NumericTolerance;
                    opts.CurveTolerance = autoMerger.CurveTolerance;
                    opts.IgnoreLimitsRotation = autoMerger.IgnoreLimitsRotation;
                    opts.IgnoreEndpointPosition = autoMerger.IgnoreEndpointPosition;
                    opts.IgnoreCurves = autoMerger.IgnoreCurves;
                    return opts;
            }
        }
    }
}
#endif

