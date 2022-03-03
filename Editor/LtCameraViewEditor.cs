using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(LtCameraView))]
public class LtCameraViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Paste Transform Matrix"))
        {
            List<float> floats = new();
            ParseStringForFloats(EditorGUIUtility.systemCopyBuffer, floats);

            while (floats.Count < 16)
            {
                floats.Add(0);
            }

            var castTarget = target as LtCameraView;
            
            ApplyFloatsAsTransform(floats, castTarget!.transform);
        }
        
        base.OnInspectorGUI();
    }

    private static void ParseStringForFloats(string argString, List<float> argOutput)
    {
        argOutput.Clear();

        argString = argString.Trim();
        argString = argString.Replace("[", "");
        argString = argString.Replace("]", "");

        var floats = argString.Split(",");

        foreach (var current in floats)
        {
            argOutput.Add(float.Parse(current));
        }
    }

    private static void ApplyFloatsAsTransform(List<float> argFloats, Transform argTransform)
    {
        if (argFloats.Count < 16) { throw new ArgumentException(nameof(argFloats)); }
        
        var matrix = new Matrix4x4
        {
            m00 = argFloats[0],
            m01 = argFloats[1],
            m02 = argFloats[2],
            m03 = argFloats[3],
            m10 = argFloats[4],
            m11 = argFloats[5],
            m12 = argFloats[6],
            m13 = argFloats[7],
            m20 = argFloats[8],
            m21 = argFloats[9],
            m22 = argFloats[10],
            m23 = argFloats[11],
            m30 = argFloats[12],
            m31 = argFloats[13],
            m32 = argFloats[14],
            m33 = argFloats[15]
        };

        argTransform.position = matrix.GetPosition();
        argTransform.rotation = matrix.rotation;
    }
}