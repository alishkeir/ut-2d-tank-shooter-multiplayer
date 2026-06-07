using Unity.Cinemachine;
using UnityEngine;

public class CameraClampExtension : CinemachineExtension
{
    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        if (CameraBounds.Instance == null)
            return;

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        Vector3 pos = state.RawPosition;

        pos.x = Mathf.Clamp(
            pos.x,
            CameraBounds.Instance.minX + halfWidth,
            CameraBounds.Instance.maxX - halfWidth);

        pos.y = Mathf.Clamp(
            pos.y,
            CameraBounds.Instance.minY + halfHeight,
            CameraBounds.Instance.maxY - halfHeight);

        state.RawPosition = pos;
    }
}