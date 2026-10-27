#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProductionEngine.EditorTools
{
    /// <summary>
    /// Production-grade project validator for Unity assets, scenes, and compilation integrity.
    /// Supports both interactive Editor execution and headless CI/CD batchmode execution.
    /// </summary>
    public static class ProjectValidator
    {
        [Serializable]
        public class ValidationResult
        {
            public string timestamp;
            public string unityVersion;
            public int totalScenesChecked;
            public int totalPrefabsChecked;
            public int missingScriptCount;
            public int danglingReferenceCount;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
            public bool passed;
        }

        [MenuItem("Tools/Engineering/Validate Project Assets & Scenes", false, 10)]
        public static void ValidateInteractive()
        {
            var result = RunFullValidation();
            string status = result.passed ? "PASSED: All checks clean!" : $"FAILED with {result.errors.Count} errors.";
            EditorUtility.DisplayDialog("Project Validation", $"{status}\n\nMissing Scripts: {result.missingScriptCount}\nDangling References: {result.danglingReferenceCount}\nReport saved to Logs/validation_report.json", "OK");
        }

        public static void RunValidationFromCommandLine()
        {
            Debug.Log("[ProjectValidator] Starting headless batch validation...");
            var result = RunFullValidation();
            if (!result.passed)
            {
                Debug.LogError($"[ProjectValidator] Batch validation FAILED with {result.errors.Count} errors.");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("[ProjectValidator] Batch validation PASSED cleanly.");
                EditorApplication.Exit(0);
            }
        }

        public static ValidationResult RunFullValidation()
        {
            var result = new ValidationResult
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                passed = true
            };

            // 1. Audit scenes in build settings
            var buildScenes = EditorBuildSettings.scenes;
            result.totalScenesChecked = buildScenes.Length;

            string currentActiveScenePath = SceneManager.GetActiveScene().path;

            foreach (var sceneEntry in buildScenes)
            {
                if (!sceneEntry.enabled) continue;

                if (!File.Exists(sceneEntry.path))
                {
                    result.errors.Add($"Scene registered in build settings does not exist: {sceneEntry.path}");
                    result.passed = false;
                    continue;
                }

                try
                {
                    var scene = EditorSceneManager.OpenScene(sceneEntry.path, OpenSceneMode.Single);
                    var rootObjects = scene.GetRootGameObjects();

                    foreach (var root in rootObjects)
                    {
                        AuditGameObjectRecursive(root, sceneEntry.path, result);
                    }
                }
                catch (Exception ex)
                {
                    result.errors.Add($"Failed to inspect scene {sceneEntry.path}: {ex.Message}");
                    result.passed = false;
                }
            }

            // Restore initial scene if valid
            if (!string.IsNullOrEmpty(currentActiveScenePath) && File.Exists(currentActiveScenePath))
            {
                EditorSceneManager.OpenScene(currentActiveScenePath, OpenSceneMode.Single);
            }

            // 2. Audit all Prefabs in project
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            result.totalPrefabsChecked = prefabGuids.Length;

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    AuditGameObjectRecursive(prefab, assetPath, result);
                }
            }

            // Save report
            try
            {
                string logsDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                string reportPath = Path.Combine(logsDir, "validation_report.json");
                string json = JsonUtility.ToJson(result, true);
                File.WriteAllText(reportPath, json);
                Debug.Log($"[ProjectValidator] Audit complete. Report written to {reportPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjectValidator] Could not write report file: {ex.Message}");
            }

            return result;
        }

        private static void AuditGameObjectRecursive(GameObject go, string sourcePath, ValidationResult result)
        {
            // Missing MonoBehaviour check
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount > 0)
            {
                result.missingScriptCount += missingCount;
                result.errors.Add($"Missing script on GameObject '{go.name}' in {sourcePath}");
                result.passed = false;
            }

            // Dangling serialized object reference check
            Component[] components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                SerializedObject so = new SerializedObject(comp);
                SerializedProperty sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                        {
                            result.danglingReferenceCount++;
                            result.warnings.Add($"Dangling reference in component '{comp.GetType().Name}' on '{go.name}' at property '{sp.propertyPath}' in {sourcePath}");
                        }
                    }
                }
            }

            // Recurse children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                AuditGameObjectRecursive(go.transform.GetChild(i).gameObject, sourcePath, result);
            }
        }
    }
}
#endif
