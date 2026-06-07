using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    public static CameraBounds Instance { get; private set; }

    [Header("World Bounds")]
    public float minX;
    public float maxX;
    public float minY;
    public float maxY;

    private void Awake()
    {
        Instance = this;
    }
}