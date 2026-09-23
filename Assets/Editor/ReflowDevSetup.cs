using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary> Dev project helpers (not part of the package). </summary>
public static class ReflowDevSetup
{
    private const string SAMPLES_SOURCE = "Packages/com.yuankunhuang.reflow/Samples~";
    private const string SAMPLES_TARGET = "Assets/Samples/Reflow/1.0.0";

    /// <summary> Once per clone: -executeMethod ReflowDevSetup.ImportTmpEssentials </summary>
    public static void ImportTmpEssentials()
    {
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
        {
            Debug.Log("[ReflowDevSetup] TMP Essential Resources already imported.");
            return;
        }
        TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Copies the package samples to where Package Manager would import them, so the smoke tests in
    /// Assets/DevTests run the current sample code. Run after editing Samples~.
    /// </summary>
    [MenuItem("Reflow Dev/Sync Samples")]
    public static void SyncSamples()
    {
        string source = Path.GetFullPath(SAMPLES_SOURCE);
        foreach (string sampleDirectory in Directory.GetDirectories(source))
        {
            string target = Path.Combine(SAMPLES_TARGET, Path.GetFileName(sampleDirectory));
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(sampleDirectory))
            {
                if (!file.EndsWith(".meta"))
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("[ReflowDevSetup] Samples synced to " + SAMPLES_TARGET);
    }
}
