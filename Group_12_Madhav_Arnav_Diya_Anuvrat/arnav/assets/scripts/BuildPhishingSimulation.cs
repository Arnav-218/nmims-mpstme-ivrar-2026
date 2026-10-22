using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildPhishingSimulation
{
    public const string GeneratedScenePath = "Assets/Scenes/PhishingSimulation.unity";

    [MenuItem("Tools/Phishing Simulation/Build Scene")]
    private static void BuildScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorUtility.DisplayDialog("Phishing Simulation", "Save the scene containing the existing XR Rig before generating the simulation.", "OK");
            return;
        }

        if (activeScene.path != GeneratedScenePath)
        {
            if (activeScene.isDirty)
            {
                EditorUtility.DisplayDialog("Phishing Simulation", "Save the scene containing the existing XR Rig before generating the simulation. The original scene will not be changed by this tool.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GeneratedScenePath) != null)
            {
                bool openExisting = EditorUtility.DisplayDialog(
                    "Generated Scene Exists",
                    "Open the existing generated scene and rebuild its simulation content? The original scene will not be changed.",
                    "Open and Rebuild",
                    "Cancel");
                if (!openExisting)
                    return;

                activeScene = EditorSceneManager.OpenScene(GeneratedScenePath, OpenSceneMode.Single);
            }
            else
            {
                if (!AssetDatabase.CopyAsset(activeScene.path, GeneratedScenePath))
                {
                    EditorUtility.DisplayDialog("Phishing Simulation", "Could not create " + GeneratedScenePath + ". Check that the Assets/Scenes folder is writable.", "OK");
                    return;
                }

                activeScene = EditorSceneManager.OpenScene(GeneratedScenePath, OpenSceneMode.Single);
            }
        }

        GameObject headObject = GameObject.Find("CenterEyeAnchor");
        if (headObject == null || headObject.transform.parent == null || headObject.transform.parent.parent == null)
        {
            EditorUtility.DisplayDialog("Phishing Simulation", "Could not find CenterEyeAnchor and its existing XR Rig in the active scene. The original scene was not changed.", "OK");
            return;
        }

        Transform rightController = headObject.transform.parent.Find("RightHandAnchor");
        if (rightController == null)
        {
            EditorUtility.DisplayDialog("Phishing Simulation", "Could not find the existing RightHandAnchor beside CenterEyeAnchor. The original scene was not changed.", "OK");
            return;
        }

        Transform rigRoot = headObject.transform.parent.parent;
        Transform existingSimulation = rigRoot.Find("Phishing Simulation");
        GameObject simulation;
        PhishingSimulationController controller;
        if (existingSimulation != null)
        {
            simulation = existingSimulation.gameObject;
            controller = simulation.GetComponent<PhishingSimulationController>();
            if (controller == null)
                controller = Undo.AddComponent<PhishingSimulationController>(simulation);
        }
        else
        {
            simulation = new GameObject("Phishing Simulation");
            Undo.RegisterCreatedObjectUndo(simulation, "Build Phishing Simulation");
            simulation.transform.SetParent(rigRoot, false);
            controller = Undo.AddComponent<PhishingSimulationController>(simulation);
        }

        controller.Configure(headObject.transform, rightController);
        controller.BuildScene();

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            EditorUtility.DisplayDialog("Phishing Simulation", "Could not save the generated scene at " + GeneratedScenePath + ".", "OK");
            return;
        }

        Selection.activeGameObject = simulation;
        Debug.Log("Built the phishing simulation in " + scene.path + ". Play the scene to begin.");
    }
}
