using UnityEngine;

public class GradientMapGeneratorData : ScriptableObject
{
    public int GradientMapResolution = 128;
    public float GradientHardness = 0.0f;
    public Gradient[] GradientList = new Gradient[1];
    public bool ShowAlpha;
    public GreyscaleSourceType SourceRemapType;
    public enum GreyscaleSourceType{Greyscale, Hue, Saturation, Brightness, SaturationAndBrightness}
}
