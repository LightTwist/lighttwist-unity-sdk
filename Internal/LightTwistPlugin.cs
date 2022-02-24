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
    private static GameObject _pluginRoot;
    
    // Scripts are not exported with the asset bundles, so this
    // is editor-only.
    [RuntimeInitializeOnLoadMethod]
    public static void BootstrapFunction()
    {
        UnityEngine.Object.Instantiate
        (
            AssetDatabase.LoadAssetAtPath<GameObject>
            (
                "Packages/com.lighttwist.lighttwistunitysdk/Internal/Prefabs/MacOsTestingBridge.prefab"
            )
        );

        // // Create objects
        // _pluginRoot = new GameObject("LtMacOsAppBridge");
        // var pluginLogic = new GameObject("PluginLogic");
        // var imagePlane = new GameObject("LtImagePlane");
        // var planePivot = new GameObject("PlanePivot");
        // var scaleOrigin = new GameObject("ScaleOrigin");
        // var plane = new GameObject("Plane");
        //
        // // Hook up parent heirarchy
        // pluginLogic.transform.parent = _pluginRoot.transform;
        // imagePlane.transform.parent = _pluginRoot.transform;
        // planePivot.transform.parent = imagePlane.transform;
        // scaleOrigin.transform.parent = planePivot.transform;
        // plane.transform.parent = scaleOrigin.transform;
        //
        // // Create components.
        // var pluginComponent = pluginLogic.AddComponent<LightTwistPlugin>();
        // var imagePlaneComponent = imagePlane.AddComponent<ImagePlaneAdjustment>();
        // var planeMesh = plane.AddComponent<MeshFilter>();
        // var planeRenderer = plane.AddComponent<MeshRenderer>();
        //
        // // Hook up references.
        // imagePlaneComponent.imageAnchor = planePivot.transform;
        // imagePlaneComponent.imageCenter = scaleOrigin.transform;
        // imagePlaneComponent.imagePlaneRenderer = planeRenderer;
    }
    
    // forward declaration of symbols to be loaded
    // symbols are loaded one by one on Awake()
    private static LightTwistPlugin instance = null;

    private byte[] _buffer = new byte[5 * 1024 * 1024];

    //public SceneLoaderComponent sceneLoader;
    
    //public Renderer TVRenderer;
    private Renderer NewTVRenderer;
    private int NewTVRendererMatIdx;
    
    // The default camera as set in the Unity Editor in the default (manager) scene.
    // For the currently rendering camera, see CurrentCamera.
    //public Camera toUnityCamera;
    //public Volume defaultVolume;
    //public Canvas loadingCanvas;
    //public RectTransform loadingImageTransform;
    
    //private const float LoadingAnimDuration = 2.0f;
    //private float loadingAnimationStartTime = -1.0f;

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

    //public static Semaphore framesInFlight;
    //private static int _maxFramesInFlight = 1;

    // I could replace LightTwistPlugin with GCHandle.ToIntPtr and manual pinning.
    // See: https://stackoverflow.com/a/32108252
    // But, in this case, we will only have one active LightTwistPlugin, so it's better to defer that complexity
    // to managing a static instance. Easier to reason about than C#'s GC rituals.
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

    private void HandleSharedTextureChanged(IntPtr tex_ref, int tex_id, int width, int height) {

        if (tex_id == 0) {
            if (tex_ref != null) {
                _segmentedPersonTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBA32, false, true, tex_ref);
                virtualCamPlaneRenderer.material.mainTexture = _segmentedPersonTexture;
            }
            else {
                Debug.Log("could not get segmented person texture");
            }
        }
        else if(tex_id == 1) {
            if (tex_ref != null) {
                _screenShareTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBA32, false, true, tex_ref);

                TryToAttachTextureToTVRenderer();
                
                // if (TVRenderer ?? false) // is null == false gives reference not assigned???
                // {
                //     TVRenderer.materials[2].mainTexture = _screenShareTexture;
                // }
            }
            else {
                Debug.Log("could not get screenshare texture");
            }
        }
    }

    // Called when the macOS app requests a new scene.
    private void HandleSceneLoadRequest(string url)
    {
        Debug.Log("Scene load requested: " + url);
        //sceneLoader.RequestNewStudio(url);
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

    // Called when SceneLoaderComponent finishes loading a requested scene on top of the default one.
    //private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene argLoadedScene)
    private void InitializeTestScene()
    {
        DisableAllCameras();
        //StopLoadingAnimation();
        //defaultVolume.gameObject.SetActive(false);

        //var rootObjects = argLoadedScene.GetRootGameObjects();
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

        // Create a new view that's identical to LtMainCamera.
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

    // private void HandleSceneUnloading()
    // {
    //     CurrentCamera = toUnityCamera;
    //     StartLoadingAnimation();
    // }
    //
    // // Called when SceneLoaderComponent finishes unloading the scene that was added on top of the default one.
    // private void HandleSceneUnloaded()
    // {
    //     defaultVolume.gameObject.SetActive(true);
    // }

    // private void StartLoadingAnimation()
    // {
    //     imagePlaneAdjustment.imagePlaneRenderer.enabled = false;
    //     loadingCanvas.enabled = true;
    //     loadingAnimationStartTime = Time.unscaledTime;
    // }

    // private void StopLoadingAnimation()
    // {
    //     imagePlaneAdjustment.imagePlaneRenderer.enabled = true;
    //     loadingCanvas.enabled = false;
    //     loadingAnimationStartTime = -1.0f;
    // }

    // private void UpdateLoadingAnimation()
    // {
    //     // If we're not loading, then don't load.
    //     if (loadingAnimationStartTime < 0.0f)
    //     {
    //         loadingImageTransform.rotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
    //     }
    //
    //     var progress = ((Time.unscaledTime - loadingAnimationStartTime) % LoadingAnimDuration) / LoadingAnimDuration;
    //     var animProgress = LtUtils.EaseBackInOut(1.0f - progress);
    //     
    //     //Debug.Log(progress + " :: " + animProgress);
    //     
    //     //loadingCanvas.transform.rotation = Quaternion.Euler(0.0f, 0.0f, 360.0f * animProgress);
    //     loadingImageTransform.rotation = Quaternion.Euler(0.0f, 0.0f, animProgress * 360.0f);
    // }

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

    // Start is called before the first frame update
    // IEnumerator Start()
    void Start()
    {
        // FROM AWAKE
        
        instance = this;

        LtSwift.RegisterSwiftMethods();

        RegisterNativeEvents();
        //RegisterGameEvents();
        
        // Depth stencil is probably not important.
        // linear/srgb only seem to affect segmented person?!

        LtSwift.init_plugin();

        LtSwift.write_to_log("LTRenderer loaded");

        //framesInFlight = new Semaphore(_maxFramesInFlight, _maxFramesInFlight);
        
        // END OF FROM AWAKE
        
        
        LtSwift.write_to_log("LightTwistPlugin::Start() was called");
        Application.runInBackground = true;
        
        DisableAllCameras();
        //StartLoadingAnimation();
        
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // If null, this will be ignored.
        // If not null, this will setup the camera.
        //CurrentCamera = toUnityCamera;
        
        // if (toUnityCamera is null == false)
        // {
        //     toUnityCamera.targetTexture = rt;
        // }

        InitializeTestScene();
        
        IntPtr texPtr = rt.GetNativeTexturePtr();
        Debug.Log("TexPtr: " + texPtr);
        LtSwift.set_shared_texture(texPtr, 0);

        // yield return StartCoroutine("CallPluginAtEndOfFrames");
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

    private int NumberOfCameraRenders = 0;
    private float FirstCameraRender = 0;
    
    // Update is called once per frame
    void Update()
    {
        // var signaled = framesInFlight.WaitOne(0);

        CheckForCameraChange();

        //if (signaled == true) {
        if (true) {
            // log("Locked framesInFlight");

            LtKeyInput.UpdateKeys();
            
            LtSwift.request_update(_buffer, _buffer.Length);
            if (segmentedPersonTexPtr == IntPtr.Zero) {
                  segmentedPersonTexPtr = GetExternalTexture(0);
            }

            if (screenShareTexPtr == IntPtr.Zero) {
                screenShareTexPtr = GetExternalTexture(1);
            }
            
            UpdatePlaneScalingValues();
            //UpdateLoadingAnimation();

            //Debug.Log("I'm in update and Current Camera is: " + (CurrentCamera is null ? "null" : "not null"));
            //if (CurrentCamera is null == false && TestCustomPass.CanRender)
            if (CurrentCamera is null == false)
            {
                NumberOfCameraRenders += 1;
                if (NumberOfCameraRenders == 1000)
                {
                    FirstCameraRender = Time.unscaledTime;
                    Debug.LogWarning("Starting to track NumberOfCameraRenders on " + Time.unscaledTime);
                }

                if (NumberOfCameraRenders == 2000)
                {
                    Debug.LogWarning("1000 camera.render occured in " + (Time.unscaledTime - FirstCameraRender) + " seconds on " + Time.unscaledTime);
                }
                
                //Debug.Log("Current camera: " + (CurrentCamera == toUnityCamera));
                //Debug.Log("Current Render Texture: " + CurrentCamera.targetTexture);
                CurrentCamera.Render();
                LightTwistPlugin.RequestTextureUpdate(0);
            }
        }
        else {
            // log("Could not lock framesInFlight");
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
        rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB);
    }

    // void Awake()
    // {
    //     instance = this;
    //
    //     LtSwift.RegisterSwiftMethods();
    //
    //     RegisterNativeEvents();
    //     //RegisterGameEvents();
    //     
    //     // Depth stencil is probably not important.
    //     // linear/srgb only seem to affect segmented person?!
    //     rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB);
    //
    //     LtSwift.init_plugin();
    //
    //     LtSwift.write_to_log("LTRenderer loaded");
    //
    //     //framesInFlight = new Semaphore(_maxFramesInFlight, _maxFramesInFlight);
    // }

    private void OnDestroy()
    {
        //UnregisterGameEvents();
        
        // If we're the current instance and we're being destroyed, then clear the current instance.
        // Otherwise, let the current instance continue to be the current instance.
        if (instance == this)
        {
            //framesInFlight = null;
            instance = null;
        }
    }

    private void RegisterNativeEvents()
    {
        LtSwift.register_keyboard_events_callback(KeyboardEvent);
        LtSwift.register_shared_texture_changed_callback(SharedTextureChanged);
        LtSwift.register_studio_load_callback(SceneLoadRequested);
    }
    
    // No scenes loading in SDK.
    // private void RegisterGameEvents()
    // {
    //     sceneLoader.sceneLoaded += HandleSceneLoaded;
    //     sceneLoader.sceneUnloading += HandleSceneUnloading;
    //     sceneLoader.sceneUnloaded += HandleSceneUnloaded;
    // }
    //
    // private void UnregisterGameEvents()
    // {
    //     sceneLoader.sceneLoaded -= HandleSceneLoaded;
    //     sceneLoader.sceneUnloading -= HandleSceneUnloading;
    //     sceneLoader.sceneUnloaded -= HandleSceneUnloaded;
    // }

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

    //    private IEnumerator CallPluginAtEndOfFrames()
	// {
	// 	while (true) {
	// 		// Wait until all frame rendering is done
	// 		yield return new WaitForEndOfFrame();
 //            
 //            LtSwift.texture_updated(0);
	// 	}
	// }
    
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