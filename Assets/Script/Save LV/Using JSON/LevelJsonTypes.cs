using System;
using UnityEngine;

[Serializable]
public struct TransformData
{
    public float px, py;
    public float rotZ;
    public float sx, sy;

    public static TransformData From(Transform t)
    {
        return new TransformData
        {
            px = t.localPosition.x,
            py = t.localPosition.y,
            rotZ = t.localEulerAngles.z,
            sx = t.localScale.x,
            sy = t.localScale.y
        };
    }

    public void ApplyTo(Transform t)
    {
        t.localPosition = new Vector3(px, py, t.localPosition.z);
        t.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        t.localScale = new Vector3(sx, sy, t.localScale.z);
    }
}

[Serializable]
public struct CellPos
{
    public int x;
    public int y;

    public CellPos(int x, int y) { this.x = x; this.y = y; }
}

[Serializable]
public class TilemapSaveData
{
    public string name;
    public TransformData transform;
    public CellPos[] cells;
}

[Serializable]
public class LevelSaveData
{
    public string levelName;
    public TransformData gridTransform;
    public TilemapSaveData[] tilemaps;
}
