using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
//using UnityEditor.ShaderGraph.Internal;
//using UnityEngine.Rendering;
using LtSwift = LtPluginNativeMethods;

internal enum ELtRequestUpdateError : int
{
    Success = 0,
    BufferTooSmall = 100
}

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX

public class LightTwistPlugin : MonoBehaviour
{
    // Scripts are not exported with the asset bundles, so this
    // is editor-only.
    [RuntimeInitializeOnLoadMethod]
    public static void BootstrapFunction()
    {
        Instantiate
        (
            AssetDatabase.LoadAssetAtPath<GameObject>
            (
                "Packages/com.lighttwist.lighttwistunitysdk/Internal/Prefabs/MacOsTestingBridge.prefab"
            )
        );
    }
    
    // forward declaration of symbols to be loaded
    // symbols are loaded one by one on Awake()
    private static LightTwistPlugin instance = null;

    private byte[] _buffer = new byte[5 * 1024 * 1024];
    
    private Renderer NewTVRenderer;
    private int NewTVRendererMatIdx;

    // Backing-field to the CurrentCamera property
    private Camera _currentCamera;

    public Camera CurrentCamera
    {
        get => _currentCamera;
        private set => TryReplaceCurrentCamera(value);
    }

    public Renderer virtualCamPlaneRenderer;

    private RenderTexture rt;
    private float[] _scalingParametersBuffer = new float[3];

    IntPtr segmentedPersonTexPtr = (IntPtr) 0;
    IntPtr screenShareTexPtr = (IntPtr) 0;

    public Texture2D _segmentedPersonTexture;
    public Texture2D _screenShareTexture;
    public ImagePlaneAdjustment imagePlaneAdjustment;

    private List<LtCameraView> _cameraViews = new();
    
    private static void KeyboardEvent(KeyCode keyCode, ELtKeycodeState state)
    {
        if (instance is null == false)
        {
            instance.HandleKeyUpdate(keyCode, state);
        }
    }
    private static void SharedTextureChanged(IntPtr tex_ref, int tex_id, int width, int height)
    {
        if (instance is null == false)
        {
            instance.HandleSharedTextureChanged(tex_ref, tex_id, width, height);
        }
    }

    private static void SceneLoadRequested(IntPtr utf8URL)
    {
        if (instance is null == false)
        {
            var url = Marshal.PtrToStringUTF8(utf8URL);
            instance.HandleSceneLoadRequest(url);
        }
    }

    private void HandleSharedTextureChanged(IntPtr tex_ref, int tex_id, int width, int height) 
    {
        if (tex_id == 0) {
            if (tex_ref != IntPtr.Zero) {
                _segmentedPersonTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBA32, false, true, tex_ref);
                virtualCamPlaneRenderer.material.mainTexture = _segmentedPersonTexture;
            }
            else {
                Debug.Log("could not get segmented person texture");
            }
        }
        else if(tex_id == 1) {
            if (tex_ref != IntPtr.Zero) {
                _screenShareTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBA32, false, true, tex_ref);

                TryToAttachTextureToTVRenderer();
            }
            else {
                Debug.Log("could not get screenshare texture");
            }
        }
    }

    // Called when the macOS app requests a new scene.
    private void HandleSceneLoadRequest(string url)
    {
        // Do nothing 
    }

    // This callback is called as a result of the ObjC call in Update().
    // Because LightTwistPlugin's execution order is before normal, scripts
    // that are normal (or later) will see all of these key states that was
    // fetched from native code.
    private void HandleKeyUpdate(KeyCode keyCode, ELtKeycodeState state)
    {
        LtSwift.write_to_log("Received keypress: "+keyCode+" - "+state);

        LtKeyInput.HandleKeyEvent(keyCode, state);

        Debug.Log("Key request for " + keyCode + " with state " + state);
    }
    
    private void InitializeTestScene()
    {
        DisableAllCameras();
        
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        
        var mainCamera = GetLtMainCamera(rootObjects);
        var planeSpots = new List<LtPlaneSpot>();
        var videoMaterials = new List<LtVideoMaterial>();
        GetLtPlaneSpots(rootObjects, planeSpots);
        GetLtVideoMaterials(rootObjects, videoMaterials);
        GetLtCameraViews(rootObjects, mainCamera, _cameraViews);

        SetupLoadedScene(mainCamera, planeSpots, videoMaterials);
    }

    private LtMainCamera GetLtMainCamera(GameObject[] argRootObjects)
    {
        foreach (var current in argRootObjects)
        {
            var cameras = current.GetComponentsInChildren<LtMainCamera>(false);
            if (cameras.Length > 0) { return cameras[0]; }
        }

        return null;
    }

    private void GetLtPlaneSpots(GameObject[] argRootObjects, List<LtPlaneSpot> argPlaneSpots)
    {
        argPlaneSpots.Clear();

        foreach (var current in argRootObjects)
        {
            argPlaneSpots.AddRange(current.GetComponentsInChildren<LtPlaneSpot>(false));
        }
    }

    private void GetLtVideoMaterials(GameObject[] argRootObjects, List<LtVideoMaterial> argVideo)
    {
        argVideo.Clear();

        foreach (var current in argRootObjects)
        {
            argVideo.AddRange(current.GetComponentsInChildren<LtVideoMaterial>(false));
        }
    }

    private void GetLtCameraViews(GameObject[] argRootObjects, LtMainCamera argCamera, List<LtCameraView> argViews)
    {
        argViews.Clear();

        // Create a new view that's identical to LtMainCamera so we can switch back.
        if (argCamera.createCameraView)
        {
            var newObject = new GameObject("NewCameraPivot");
            var newCamera = newObject.AddComponent<Camera>();
            var mainCamera = argCamera.GetComponent<Camera>();
            var newView = newObject.AddComponent<LtCameraView>();

            newCamera.enabled = false;
            newCamera.fieldOfView = mainCamera.fieldOfView;
            newView.relevantCamera = newCamera;

            var newTransform = newObject.transform;
            var camTransform = argCamera.transform;
            newTransform.position = camTransform.position;
            newTransform.rotation = camTransform.rotation;
            argViews.Add(newView);
        }
        
        foreach (var current in argRootObjects)
        {
            argViews.AddRange(current.GetComponentsInChildren<LtCameraView>(false));
        }
    }

    private void SetupLoadedScene
    (
        LtMainCamera argCamera,
        List<LtPlaneSpot> argPlaneSpots,
        List<LtVideoMaterial> argVideo
    )
    {
        if (argCamera is null == false)
        {
            CurrentCamera = argCamera.GetComponent<Camera>();
        }

        Debug.Log("Setting up Loaded Scene: " + argPlaneSpots.Count);

        foreach (var current in argPlaneSpots)
        {
            Debug.Log(current.name);
            // Debug.Log(current.transform.position);
            // Debug.Log(current.transform.rotation);
        }
        
        if (argPlaneSpots.Count > 0)
        {
            var currentSpot = argPlaneSpots[0];
            var spotTransform = currentSpot.transform;
            var planeTransform = imagePlaneAdjustment.transform;

            planeTransform.position = spotTransform.position;
            planeTransform.rotation = spotTransform.rotation;
            imagePlaneAdjustment.SetEmissionStrength(currentSpot.emissionMultiplier);
        }

        if (argVideo.Count > 0)
        {
            var firstMarker = argVideo[0];
            NewTVRenderer = firstMarker.affectedRenderer;
            NewTVRendererMatIdx = firstMarker.materialIndex;
            TryToAttachTextureToTVRenderer();
        }
    }

    private void TryToAttachTextureToTVRenderer()
    {
        if (NewTVRenderer != null)
        {
            NewTVRenderer.materials[NewTVRendererMatIdx].mainTexture = _screenShareTexture;
        }
    }

    public static void RequestTextureUpdate(int tex_id)
    {
        // Skip updating the texture if we're not in Play mode.
        if (instance is null == false)
        {
            notify_texture_updated(tex_id);
        }
    }
    
    private static void notify_texture_updated(int tex_id) {
#if UNITY_EDITOR_OSX && false
        if (LtSwift.texture_updated != null) {
            LtSwift.texture_updated(tex_id);
        }
#else
        LtSwift.texture_updated(tex_id);
#endif
    }
    
    void Start()
    {
        // FROM AWAKE
        
        instance = this;

        LtSwift.RegisterSwiftMethods();

        RegisterNativeEvents();
        
        LtSwift.init_plugin();
        LtSwift.write_to_log("LTRenderer loaded");
        
        // END OF FROM AWAKE

        LtSwift.write_to_log("LightTwistPlugin::Start() was called");
        Application.runInBackground = true;
        
        DisableAllCameras();

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        InitializeTestScene();
        
        // After InitializeTestScene() binds rt to the camera.
        IntPtr texPtr = rt.GetNativeTexturePtr();
        LtSwift.set_shared_texture(texPtr, 0);
        
    }

    void CheckForCameraChange()
    {
        foreach (var currentView in _cameraViews)
        {
            if (LtKeyInput.WasKeyDownThisFrame(currentView.inputToSwitch))
            {
                SwitchToNewCamera(currentView);
                break;
            }
        }
    }

    void SwitchToNewCamera(LtCameraView argSelectedView)
    {
        var ourCamTransform = CurrentCamera.transform;
        var viewTransform = argSelectedView.transform;

        ourCamTransform.position = viewTransform.position;
        ourCamTransform.rotation = viewTransform.rotation;
        CurrentCamera.fieldOfView = argSelectedView.relevantCamera.fieldOfView;
    }

    // Update is called once per frame
    void Update()
    {
        CheckForCameraChange();
        
        LtKeyInput.UpdateKeys();
        
        LtSwift.request_update(_buffer, _buffer.Length);
        if (segmentedPersonTexPtr == IntPtr.Zero) {
              segmentedPersonTexPtr = GetExternalTexture(0);
        }

        if (screenShareTexPtr == IntPtr.Zero) {
            screenShareTexPtr = GetExternalTexture(1);
        }
        
        UpdatePlaneScalingValues();
        
        //if (CurrentCamera is null == false && TestCustomPass.CanRender)
        if (CurrentCamera is null == false)
        {
            CurrentCamera.Render();
            RequestTextureUpdate(0);
        }
    }

    IntPtr GetExternalTexture(int tex_id)
    {
        var widthHeight = new int[2];
        IntPtr tex_ref = LtSwift.get_shared_texture(tex_id, widthHeight);
        HandleSharedTextureChanged(tex_ref, tex_id, widthHeight[0], widthHeight[1]);
        return tex_ref;
    }

    void UpdatePlaneScalingValues()
    {
        LtSwift.get_scaling_parameters(_scalingParametersBuffer);
        if (imagePlaneAdjustment is null == false)
        {
            imagePlaneAdjustment.imagePositionHeight = _scalingParametersBuffer[0];
            imagePlaneAdjustment.imageScaleWidth = _scalingParametersBuffer[1];
            imagePlaneAdjustment.imageScaleHeight = _scalingParametersBuffer[2];
        }
    }

    private void Awake()
    {
        // Depth stencil is probably not important.
        // linear/srgb only seem to affect segmented person?!
        rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB);
    }

    private void OnDestroy()
    {
        // If we're the current instance and we're being destroyed, then clear the current instance.
        // Otherwise, let the current instance continue to be the current instance.
        if (instance == this)
        {
            instance = null;
        }
    }

    private void RegisterNativeEvents()
    {
        LtSwift.register_keyboard_events_callback(KeyboardEvent);
        LtSwift.register_shared_texture_changed_callback(SharedTextureChanged);
        LtSwift.register_studio_load_callback(SceneLoadRequested);
    }

    private void TryReplaceCurrentCamera(Camera argNewCamera)
    {
        if (argNewCamera is null) { return; }
        if (argNewCamera == _currentCamera) { return; }

        if (_currentCamera is null == false)
        {
            _currentCamera.targetTexture = null;
        }

        _currentCamera = argNewCamera;
        SetupCurrentCamera(argNewCamera);
    }

    private void SetupCurrentCamera(Camera argNewCamera)
    {
        Debug.Log("Setting target texture to rt: " + rt);
        argNewCamera.targetTexture = rt;
    }

    private static void DisableAllCameras()
    {
        foreach (var currentCamera in Camera.allCameras)
        {
            currentCamera.enabled = false;
        }
    }
    
    void OnApplicationQuit()
    {
        LtSwift.OnApplicationQuit();
    }
}

#else

public class LightTwistPlugin : MonoBehaviour
{

}

#endif