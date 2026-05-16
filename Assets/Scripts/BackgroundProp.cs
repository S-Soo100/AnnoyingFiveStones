using UnityEngine;

public enum BackgroundPropShape { Cube, Sphere, Cylinder, Capsule }

[System.Serializable]
public struct BackgroundProp
{
    public BackgroundPropShape Shape;
    public Color Color;
    public Vector3 Position;   // 월드 좌표
    public Vector3 Scale;
}
