using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;


    [SerializeField] private int cameraOwnerPriority = 20;


    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(); // a tring value that is synced over the network

    public override void OnNetworkSpawn()
    {

        if (IsServer)
        {
            UserData userData = HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);
            PlayerName.Value = userData.userName;
        }

        if (IsOwner)
        {
            cinemachineCamera.Priority = cameraOwnerPriority;
            cinemachineCamera.Prioritize();
        }
    }
}
