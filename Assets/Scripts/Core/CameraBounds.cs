// CameraBounds
// ------------
// A simple singleton that defines the rectangular play area of the level
// in world space. It is used by CameraClampExtension to keep the camera
// from showing anything outside the level, and can be referenced by any
// other system that needs to know the playable world bounds.
//
// Setup:
// - Attach this component to a single GameObject in the scene (often empty).
// - In the inspector, set minX/maxX/minY/maxY to the world-space rectangle
//   the player should not be able to see beyond.
//
// Access:
// - CameraBounds.Instance.minX / maxX / minY / maxY
using UnityEngine;

public class CameraBounds : MonoBehaviour
{
    // Global access point. Assigned in Awake.
    public static CameraBounds Instance { get; private set; }

    [Header("World Bounds")]
    public float minX; // Minimum world-space X coordinate visible to the camera.
    public float maxX; // Maximum world-space X coordinate visible to the camera.
    public float minY; // Minimum world-space Y coordinate visible to the camera.
    public float maxY; // Maximum world-space Y coordinate visible to the camera.

    private void Awake()
    {
        // Promote this object to the global instance.
        Instance = this;
    }
}
