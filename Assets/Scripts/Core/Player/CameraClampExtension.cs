// CameraClampExtension
// --------------------
// A Cinemachine extension that prevents the virtual camera from showing anything
// outside the rectangle defined by CameraBounds.
//
// How it works:
// - Hooks into Cinemachine's Finalize pipeline stage.
// - Reads the main camera's orthographic size/aspect to know how much world the
//   camera covers in each direction.
// - Clamps the raw position so the camera frustum stays within CameraBounds
//   (taking the half-width/half-height into account).
//
// Requirements:
// - A CameraBounds singleton must be present in the scene.
// - The active camera must be orthographic.
using Unity.Cinemachine;
using UnityEngine;

public class CameraClampExtension : CinemachineExtension
{
    // Called by Cinemachine at the configured pipeline stage.
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        // Only act on the final position, after all other stages are done.
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        // No bounds defined - nothing to clamp against.
        if (CameraBounds.Instance == null)
            return;

        // Only orthographic cameras are supported by this extension.
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        // Compute the camera's half-extents in world units.
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        Vector3 pos = state.RawPosition;

        // Clamp X so the camera's left/right edges stay inside the play area.
        pos.x = Mathf.Clamp(
            pos.x,
            CameraBounds.Instance.minX + halfWidth,
            CameraBounds.Instance.maxX - halfWidth);

        // Clamp Y so the camera's top/bottom edges stay inside the play area.
        pos.y = Mathf.Clamp(
            pos.y,
            CameraBounds.Instance.minY + halfHeight,
            CameraBounds.Instance.maxY - halfHeight);

        // Apply the clamped position back to the camera state.
        state.RawPosition = pos;
    }
}
