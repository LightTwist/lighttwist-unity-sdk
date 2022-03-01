using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

[Serializable]
public class MetaData
{
    public string version;
    public string studio_name;
    public string studio_id;

    public List<StudioSpot> spots = new();
}

[Serializable]
public class StudioSpot
{
    public string name;
    public string id;
    public List<float> camera_transform = new();
}

public class LtStudioMetadata : EditorWindow
{
    private string assetBundlePath = "";
    
    private string jsonStudioVersion = "1.0";
    private string jsonStudioName = "";
    private string jsonStudioId = "";

    private string studioFilePrefix = "";

    [MenuItem("Window/LightTwist/Package Asset Bundle Into Studio")]
    private static void ShowWindow()
    {
        var window = GetWindow<LtStudioMetadata>();
        window.titleContent = new GUIContent("Package LtStudio");
        window.Show();
    }

    private void OnGUI()
    {
        const string tempFolderName = "LtStudioCreate";
        const string thumbnailName = "thumbnail.jpg";
        
        GUILayout.Label("Packaging AssetBundle as Studio", EditorStyles.boldLabel);
        
        EditorGUILayout.Space(16);
        if (string.IsNullOrWhiteSpace(assetBundlePath))
        {
            GUILayout.Label("Please select an asset bundle");
        }
        else
        {
            GUILayout.Label(assetBundlePath, EditorStyles.wordWrappedLabel);
        }

        if (GUILayout.Button("Select Asset Bundle"))
        {
            assetBundlePath = EditorUtility.OpenFilePanel("Select Asset Bundle", "", "");
        }
        EditorGUILayout.Space(10);
        jsonStudioVersion = EditorGUILayout.TextField("(Metadata) Studio Version", jsonStudioVersion);
        jsonStudioName = EditorGUILayout.TextField("(Metadata) Studio Display Name", jsonStudioName);
        jsonStudioId = EditorGUILayout.TextField("(Metadata) Studio ID", jsonStudioId);

        studioFilePrefix = EditorGUILayout.TextField("Studio File Name Prefix", studioFilePrefix);

        EditorGUILayout.Space(20);
        if (GUILayout.Button("Validate Studio"))
        {
            if (string.IsNullOrWhiteSpace(assetBundlePath))
            {
                throw new Exception("Asset Bundle Path is not set");
            }

            if (File.Exists(assetBundlePath) == false)
            {
                throw new Exception("Asset Bundle Path does not lead to a file");
            }
            
            if (File.Exists(Path.Combine(tempFolderName, thumbnailName)) == false)
            {
                throw new Exception($"Thumbnail does not exist. Please place a {thumbnailName} in a folder called {tempFolderName} that's in your project folder (as a sibling of Assets, Library, Packages, ProjectSettings, etc.).");
            }

            var currentRootObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects();

            List<LtMainCamera> mainCameras = new();
            
            foreach (var currentRoot in currentRootObjects)
            {
                mainCameras.AddRange(currentRoot.GetComponents<LtMainCamera>());
            }

            if (mainCameras.Count < 1)
            {
                throw new Exception("The world needs at least one main camera");
            }

            if (mainCameras.Count > 1)
            {
                throw new Exception("The world cannot have more than one main camera");
            }
            
            Debug.Log("Validation complete. Studio is valid.");
        }
        
        if (GUILayout.Button("Build Studio"))
        {
            var tempFolderPath = Path.Combine(Path.GetTempPath(), tempFolderName);
            var studioFolderName = $"{studioFilePrefix}.ltstudio";
            var studioFolderPath = Path.Combine(tempFolderPath, studioFolderName);
            CreateOrEmptyDirectory(tempFolderPath);
            Directory.CreateDirectory(studioFolderPath);

            const string metadataJsonName = "metadata.json";
            const string studioFileName = "studio-unity";
            
            // Fill folder with relevant files
            File.Copy(assetBundlePath, Path.Combine(studioFolderPath, studioFileName));
            File.Copy(Path.Combine(tempFolderName, thumbnailName), Path.Combine(studioFolderPath, thumbnailName));
            CreateSettingsFile(Path.Combine(studioFolderPath, metadataJsonName));

            var outputZip = $"{studioFolderName}.zip";
            if (File.Exists(outputZip))
            {
                File.Delete(outputZip);
            }
            
            // Zip folder into asset bundle
            //ZipFile.CreateFromDirectory(tempFolderPath, outputZip);
            CreateZipFromFolder(tempFolderPath, outputZip);
            File.Copy(Path.Combine(tempFolderPath, outputZip), outputZip);
        }
    }

    private static void CreateOrEmptyDirectory(string argDesiredPath)
    {
        if (Directory.Exists(argDesiredPath))
        {
            Directory.Delete(argDesiredPath, true);
        }

        Directory.CreateDirectory(argDesiredPath);
    }

    private void CreateSettingsFile(string argPath)
    {
        var metadata = new MetaData();
        metadata.version = jsonStudioVersion;
        metadata.studio_name = jsonStudioName;
        metadata.studio_id = jsonStudioId;
        
        var currentRootObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        
        List<LtMainCamera> mainCameras = new();
            
        foreach (var currentRoot in currentRootObjects)
        {
            mainCameras.AddRange(currentRoot.GetComponents<LtMainCamera>());
        }
        
        List<LtCameraView> cameraViews = new();
            
        foreach (var currentRoot in currentRootObjects)
        {
            cameraViews.AddRange(currentRoot.GetComponents<LtCameraView>());
        }

        if (mainCameras.Count != 1)
        {
            throw new Exception("Exactly one LtMainCamera must be present in the scene.");
        }

        if (mainCameras[0].createCameraView)
        {
            var newSpot = new StudioSpot();
            newSpot.name = mainCameras[0].spotName;
            newSpot.id = mainCameras[0].spotId;

            var camTransform = mainCameras[0].transform;
            var transformMatrix = Matrix4x4.TRS(camTransform.position, camTransform.rotation, camTransform.localScale);
            FillListWithTransform(transformMatrix, newSpot.camera_transform);
            metadata.spots.Add(newSpot);
        }

        foreach (var currentView in cameraViews)
        {
            var newSpot = new StudioSpot();
            newSpot.name = currentView.spotName;
            newSpot.id = currentView.spotId;

            var camTransform = currentView.transform;
            var transformMatrix = Matrix4x4.TRS(camTransform.position, camTransform.rotation, camTransform.localScale);
            FillListWithTransform(transformMatrix, newSpot.camera_transform);
            metadata.spots.Add(newSpot);
        }

        var output = JsonUtility.ToJson(metadata, true);
        File.WriteAllText(argPath, output);

        // var lines = new[]
        // {
        //     "{",
        //     $"   \"version\": \"{jsonStudioVersion}\",",
        //     $"   \"studio_name\": \"{jsonStudioName}\",",
        //     $"   \"studio_id\": \"{jsonStudioId}\"",
        //     "}"
        // };
        //
        // File.WriteAllLines(argPath, lines);
    }

    private static void CreateZipFromFolder(string argFolderToZip, string argZipToCreate)
    {
#if UNITY_EDITOR_WIN
        CreateZipFromFolderWindowsEditor(argFolderToZip, argZipToCreate);
#else
        CreateZipFromFolderPosix(argFolderToZip, argZipToCreate);
#endif
    }

    private static void CreateZipFromFolderWindowsEditor(string argFolderToZip, string argZipToCreate)
    {
        //string binPath = Path.Combine(Application.dataPath, "Editor", "third-party", "7zip", "windows");
        string binPath = "Packages/com.lighttwist.lighttwistunitysdk/Editor/third-party/7zip/windows";
        var processInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(binPath, "7za.exe"),
            WorkingDirectory = argFolderToZip,
            Arguments = $"a {argZipToCreate} *"
        };

        using var zipProcess = Process.Start(processInfo);
        zipProcess?.WaitForExit();
    }

    private static void CreateZipFromFolderPosix(string argFolderToZip, string argZipToCreate)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "zip",
            WorkingDirectory = argFolderToZip,
            Arguments = $"-r {argZipToCreate} ."
        };

        using var zipProcess = Process.Start(processInfo);
        zipProcess?.WaitForExit();
    }

    private static void FillListWithTransform(Matrix4x4 argMatrix, List<float> argFloats)
    {
        argFloats.Clear();
        
        argFloats.Add(argMatrix.m00);
        argFloats.Add(argMatrix.m01);
        argFloats.Add(argMatrix.m02);
        argFloats.Add(argMatrix.m03);
        argFloats.Add(argMatrix.m10);
        argFloats.Add(argMatrix.m11);
        argFloats.Add(argMatrix.m12);
        argFloats.Add(argMatrix.m13);
        argFloats.Add(argMatrix.m20);
        argFloats.Add(argMatrix.m21);
        argFloats.Add(argMatrix.m22);
        argFloats.Add(argMatrix.m23);
        argFloats.Add(argMatrix.m30);
        argFloats.Add(argMatrix.m31);
        argFloats.Add(argMatrix.m32);
        argFloats.Add(argMatrix.m33);
    }
}
