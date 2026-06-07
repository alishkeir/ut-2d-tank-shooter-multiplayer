using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    public static CameraBounds Instance { get; private set; }

    [Header("World Bounds")]
    [SerializeField] public float minX;
    [SerializeField] public float maxX;
    [SerializeField] public float minY;
    [SerializeField] public float maxY;

    private void Awake()
    {
        Instance = this;
    }
}