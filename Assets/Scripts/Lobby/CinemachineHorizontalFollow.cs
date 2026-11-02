using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class CinemachineHorizontalFollow : CinemachineExtension
{
    private bool hasFixedHeight;
    private float fixedHeight;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        if (!hasFixedHeight)
        {
            fixedHeight = state.GetFinalPosition().y;
            hasFixedHeight = true;
        }

        Vector3 correction = state.PositionCorrection;
        correction.y = fixedHeight - state.RawPosition.y;
        state.PositionCorrection = correction;
    }
}
