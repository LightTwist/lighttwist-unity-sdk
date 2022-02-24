using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class LtPluginNativeMethods
{
    
    #if UNITY_EDITOR_OSX && false

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr get_shared_texture_t(int tex_id, int[] dimensions); //dimensions: width, height
    internal static get_shared_texture_t get_shared_texture;

    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
    internal delegate IntPtr set_shared_texture_t(IntPtr texPtr, int id);
    internal static set_shared_texture_t set_shared_texture;

    [UnmanagedFunctionPointerAttribute(CallingConvention.Cdecl)]
    internal delegate IntPtr texture_updated_t(int id);
    internal static texture_updated_t texture_updated;
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int teardown_plugin_t();
    internal static teardown_plugin_t teardown_plugin;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int init_plugin_t();
    internal static init_plugin_t init_plugin;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int setup_webcam_mach_port_t();
    internal static setup_webcam_mach_port_t setup_webcam_port;
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int clear_webcam_port_uint_t();
    internal static clear_webcam_port_uint_t clear_webcam_uint;
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void get_scaling_parameters_t(float[] scaling_parameters); //scaling_parameters: elevation, width, height
    internal static get_scaling_parameters_t get_scaling_parameters;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void register_keyboard_events_callback_t(key_callback_t callback);
    internal static register_keyboard_events_callback_t register_keyboard_events_callback;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void register_shared_texture_changed_callback_t(shared_texture_changed_callback_t callback);
    internal static register_shared_texture_changed_callback_t register_shared_texture_changed_callback;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void register_studio_load_callback_t(studio_load_callback_t callback);
    internal static register_studio_load_callback_t register_studio_load_callback;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate ELtRequestUpdateError request_update_t(byte[] buffer, int bufferSize);
    internal static request_update_t request_update;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void key_callback_t(KeyCode keycode, ELtKeycodeState state);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void shared_texture_changed_callback_t(IntPtr tex_ref, int tex_id, int width, int height);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void studio_load_callback_t(IntPtr utf8URL);
#endif

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR_OSX) || true
    [DllImport("libLTUnityBridge")]
    internal static extern IntPtr get_shared_texture(int tex_id, int[] dimensions);

    [DllImport("libLTUnityBridge")]
    internal static extern IntPtr set_shared_texture(IntPtr texPtr, int id);

    [DllImport("libLTUnityBridge")]
    internal static extern IntPtr texture_updated(int id);

    [DllImport("libLTUnityBridge")]
    internal static extern int teardown_plugin();

    [DllImport("libLTUnityBridge")]
    internal static extern int init_plugin();

    [DllImport("libLTUnityBridge")]
    internal static extern int setup_webcam_port();

    [DllImport("libLTUnityBridge")]
    internal static extern int clear_webcam_port();

    [DllImport("libLTUnityBridge")]
    internal static extern void get_scaling_parameters(float[] scaling_parameters);
    
    [DllImport("libLTUnityBridge")]
    internal static extern void register_keyboard_events_callback(key_callback_t callback);

    [DllImport("libLTUnityBridge")]
    internal static extern void register_shared_texture_changed_callback(shared_texture_changed_callback_t callback);

    [DllImport("libLTUnityBridge")]
    internal static extern void register_studio_load_callback(studio_load_callback_t callback);
    
    [DllImport("libLTUnityBridge")]
    internal static extern ELtRequestUpdateError request_update(byte[] buffer, int bufferSize);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void key_callback_t(KeyCode keycode, ELtKeycodeState state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void shared_texture_changed_callback_t(IntPtr tex_ref, int tex_id, int width, int height);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void studio_load_callback_t(IntPtr utf8URL);
#endif

    [DllImport("libLTUnityBridge", CharSet = CharSet.Ansi)]
    internal static extern void write_to_log(string str);

    internal static void RegisterSwiftMethods()
    {
    // #if UNITY_EDITOR_OSX
    //     var libpath = get_unique_library_instance(interface_lib);
    //     get_shared_texture = FunctionLoader.LoadFunction<get_shared_texture_t>(libpath, "get_shared_texture");
    //     set_shared_texture = FunctionLoader.LoadFunction<set_shared_texture_t>(libpath, "set_shared_texture");
    //     texture_updated = FunctionLoader.LoadFunction<texture_updated_t>(libpath, "texture_updated");
    //     teardown_plugin = FunctionLoader.LoadFunction<teardown_plugin_t>(libpath, "teardown_plugin");
    //     init_plugin = FunctionLoader.LoadFunction<init_plugin_t>(libpath, "init_plugin");
    //     setup_webcam_port = FunctionLoader.LoadFunction<setup_webcam_mach_port_t>(libpath, "setup_webcam_port");
    //     clear_webcam_uint = FunctionLoader.LoadFunction<clear_webcam_port_uint_t>(libpath, "clear_webcam_port_uint");
    //     //update_plugin = FunctionLoader.LoadFunction<update_plugin_t>(libpath, "update_plugin");
    //     get_scaling_parameters =
    //         FunctionLoader.LoadFunction<get_scaling_parameters_t>(libpath, "get_scaling_parameters");
    //     register_keyboard_events_callback =
    //         FunctionLoader.LoadFunction<register_keyboard_events_callback_t>(libpath,
    //             "register_keyboard_events_callback");
    //     register_shared_texture_changed_callback =
    //         FunctionLoader.LoadFunction<register_shared_texture_changed_callback_t>(libpath,
    //             "register_shared_texture_changed_callback");
    //     register_studio_load_callback =
    //         FunctionLoader.LoadFunction<register_studio_load_callback_t>(libpath, "register_studio_load_callback");
    //     request_update = FunctionLoader.LoadFunction<request_update_t>(libpath, "request_update");
    // #endif
    }
    
    #if !UNITY_EDITOR_OSX
    public static void OnApplicationQuit()
    {
    
    }
    #endif
    
    #if UNITY_EDITOR_OSX
    ////////////////////////////////////////////
    // below here it's just dylib plumbing
    const string interface_lib = "Assets/NativeLibrary/libLTUnityBridge.dylib";

    public static void OnApplicationQuit()
    {
        foreach (var p in temp_libs)
        {
            Debug.Log("Cleaning up " + p);
            File.Delete(p);
        }
    
        var machPortError = teardown_plugin();
        Debug.Log(machPortError == 0 ? "Successfully closed mach port" : "Failed to close mach port");
    
        FunctionLoader.FreeLibraries();
    }

    static List<string> temp_libs = new List<string>();
    static string get_unique_library_instance(string orig_path)
    {
        var unique_path = Path.GetTempFileName() + "_" + Path.GetFileName(orig_path);

        File.Copy(orig_path, unique_path);

        Debug.Log("Copied " + orig_path + " to " + unique_path);
        temp_libs.Add(unique_path);

        return unique_path;
    // make sure we clean up later
    }

    class FunctionLoader
    {
        public static T LoadFunction<T>(string dllPath, string functionName) where T : Delegate
        {
            var hModule = dl_native.LoadLibrary(dllPath);
            handles.Add(hModule);
            var functionAddress = dl_native.GetProcAddress(hModule, functionName);
            return (T)Marshal.GetDelegateForFunctionPointer(functionAddress, typeof (T));
        }

    // tracking what we loaded for easier cleanup
        static List<IntPtr> handles = new List<IntPtr>();

        public static void FreeLibraries()
        {
            foreach (var ptr in handles)
            {
                Debug.Log("Cleaning up module " + ptr);
    // todo: check if ptr was -1 or something bad like that...
                dl_native.FreeLibrary(ptr);
            }
        }
    }

    class dl_native
    {

        public static IntPtr LoadLibrary(string fileName) {
            return dlopen(fileName, RTLD_NOW);
        }

        public static void FreeLibrary(IntPtr handle) {
            dlclose(handle);
        }

        public static IntPtr GetProcAddress(IntPtr dllHandle, string name) {
            // clear previous errors if any
            dlerror();
            var res = dlsym(dllHandle, name);
            var errPtr = dlerror();
            if (errPtr != IntPtr.Zero) {
                throw new Exception("dlsym: " + Marshal.PtrToStringAnsi(errPtr));
            }
            return res;
        }

        const int RTLD_NOW = 2;

        [DllImport("libdl.dylib")]
        private static extern IntPtr dlopen(String fileName, int flags);

        [DllImport("libdl.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, String symbol);

        [DllImport("libdl.dylib")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.dylib")]
        private static extern IntPtr dlerror();

    }
    #endif
}