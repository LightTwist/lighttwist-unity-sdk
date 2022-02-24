using System;
using UnityEngine;

[ExecuteAlways]
public class ImagePlaneAdjustment : MonoBehaviour
{
    public float imageScaleHeight;
    public float imageScaleWidth;
    public float imagePositionHeight;

    public Transform imageAnchor;
    public Transform imageCenter;
    public Renderer imagePlaneRenderer;

    private readonly int _emissionParameterId = Shader.PropertyToID("_Multiplier");

    private void Update()
    {
        if (imageAnchor is null == false)
        {
            imageAnchor.localPosition = Vector3.up * imagePositionHeight;
            imageCenter.localScale = new Vector3(imageScaleWidth, imageScaleHeight, 1.0f);
        }
    }

    public void SetEmissionStrength(float argEmissionStrength)
    {
        imagePlaneRenderer.material.SetFloat(_emissionParameterId, argEmissionStrength);
    }
}
