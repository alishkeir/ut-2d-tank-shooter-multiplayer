using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;


    [SerializeField] private int cameraOwnerPriority = 20;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            cinemachineCamera.Priority = cameraOwnerPriority;
            cinemachineCamera.Prioritize();
        }
    }
}
