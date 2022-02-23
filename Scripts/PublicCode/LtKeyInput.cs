using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ELtKeycodeState
{
    Up = 0,
    Down = 1
}

public static class LtKeyInput
{
    // readonly means the reference to the container.
    // The dictionary can be cleared and added to.
    private static readonly List<(KeyCode, ELtKeycodeState)> _keycodeStatesThisFrame = new();
    private static readonly Dictionary<KeyCode, ELtKeycodeState> _keycodeStatesOverTime = new();
    
    private static readonly KeyCode[] AllKeycodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().ToArray();

    public static void HandleKeyEvent(KeyCode argKeyCode, ELtKeycodeState argState)
    {
        _keycodeStatesThisFrame.Add((argKeyCode, argState));
        _keycodeStatesOverTime[argKeyCode] = argState;
    }
    
    public static bool WasKeyDownThisFrame(KeyCode argKeyCode)
    {
        var isInContainer = _keycodeStatesThisFrame.Contains((argKeyCode, ELtKeycodeState.Down));
        return isInContainer;
    }
    
    public static bool WasKeyUpThisFrame(KeyCode argKeyCode)
    {
        var isInContainer = _keycodeStatesThisFrame.Contains((argKeyCode, ELtKeycodeState.Up));
        return isInContainer;
    }
    
    public static ELtKeycodeState GetCurrentState(KeyCode argKeyCode)
    {
        // Assume that, if we never seen the button state, that it's up (never been pushed down).
        if (_keycodeStatesOverTime.TryGetValue(argKeyCode, out var output))
        {
            return output;
        }
        
        return ELtKeycodeState.Up;
    }

    public static void UpdateKeys()
    {
        _keycodeStatesThisFrame.Clear();
        FillKeyStatesFromUnity();
    }

    public static void Reset()
    {
        _keycodeStatesThisFrame.Clear();
        _keycodeStatesOverTime.Clear();
    }
    
    private static void FillKeyStatesFromUnity()
    {
        foreach (var currentKeycode in AllKeycodes)
        {
            var wasDownThisFrame = false;
            var wasUpThisFrame = false;
            if (Input.GetKeyDown(currentKeycode))
            {
                Debug.Log("Adding Down for " + currentKeycode + " from Unity");
                wasDownThisFrame = true;
                _keycodeStatesOverTime[currentKeycode] = ELtKeycodeState.Down;
                _keycodeStatesThisFrame.Add((currentKeycode, ELtKeycodeState.Down));
            }

            if (Input.GetKeyUp(currentKeycode))
            {
                Debug.Log("Adding Up for " + currentKeycode + " from Unity");
                wasUpThisFrame = true;
                _keycodeStatesOverTime[currentKeycode] = ELtKeycodeState.Up;
                _keycodeStatesThisFrame.Add((currentKeycode, ELtKeycodeState.Up));
            }

            if (wasDownThisFrame && wasUpThisFrame)
            {
                _keycodeStatesOverTime[currentKeycode] = ELtKeycodeState.Up;
            }
        }
    }
}