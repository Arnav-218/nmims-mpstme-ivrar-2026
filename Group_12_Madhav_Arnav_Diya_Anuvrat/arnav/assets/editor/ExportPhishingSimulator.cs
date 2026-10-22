using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ExportPhishingSimulator
{
    private const string PackageRelativePath = "Exports/PhishingSimulator.unitypackage";

    [MenuItem("Tools/Phishing Simulation/Export Package")]
    private static void ExportPackage()
    {
        string[] assetPaths =
        {
            BuildPhishingSimulation.GeneratedScenePath,
            "Assets/Scripts/PhishingSimulationController.cs",
            "Assets/Scripts/PhishingActionButton.cs",
            "Assets/Editor/BuildPhishingSimulation.cs",
            "Assets/Editor/ExportPhishingSimulator.cs"
        };

        for (int i = 0; i < assetPaths.Length; i++)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPaths[i]) == null)
            {
                EditorUtility.DisplayDialog(
                    "Phishing Simulation Export",
                    "Required asset is missing: " + assetPaths[i] +
                    (assetPaths[i] == BuildPhishingSimulation.GeneratedScenePath
                        ? "\n\nGenerate and save the scene first using Tools > Phishing Simulation > Build Scene."
                        : ""),
                    "OK");
                return;
            }
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sceneFilePath = Path.Combine(projectRoot, BuildPhishingSimulation.GeneratedScenePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            string sceneContents = File.ReadAllText(sceneFilePath);
            if (!sceneContents.Contains("sceneBuildVersion: " + PhishingSimulationController.CurrentSceneBuildVersion))
            {
                EditorUtility.DisplayDialog(
                    "Phishing Simulation Export",
                    "The generated scene is missing the current simulation content or is out of date.\n\nRun Tools > Phishing Simulation > Build Scene, then export again.",
                    "OK");
                return;
            }
        }
        catch (IOException exception)
        {
            EditorUtility.DisplayDialog("Phishing Simulation Export", "Could not read the generated scene:\n" + exception.Message, "OK");
            return;
        }
        catch (UnauthorizedAccessException exception)
        {
            EditorUtility.DisplayDialog("Phishing Simulation Export", "Could not read the generated scene:\n" + exception.Message, "OK");
            return;
        }

        string packagePath = Path.GetFullPath(Path.Combine(projectRoot, PackageRelativePath));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
            AssetDatabase.ExportPackage(assetPaths, packagePath, ExportPackageOptions.IncludeDependencies);

            FileInfo packageFile = new FileInfo(packagePath);
            if (!packageFile.Exists || packageFile.Length == 0)
            {
                EditorUtility.DisplayDialog("Phishing Simulation Export", "Unity did not create the package at " + packagePath + ".", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Phishing Simulation Export", "Package exported successfully:\n" + packagePath +
                "\n\nThe target project must have Meta XR SDK 207.0.1 installed.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Phishing Simulation Export", "Package export failed:\n" + exception.Message, "OK");
        }
    }
}
