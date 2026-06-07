using System;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [field: SerializeField] public Health Health { get; private set; }

    [SerializeField] private int cameraOwnerPriority = 20;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(); // a tring value that is synced over the network

    public static event Action<Player> OnPlayerSpawned;
    public static event Action<Player> OnPlayerDespawned;


    public override void OnNetworkSpawn()
    {

        if (IsServer)
        {
            UserData userData = HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);
            PlayerName.Value = userData.userName;

            OnPlayerSpawned?.Invoke(this);
        }

        if (IsOwner)
        {
            cinemachineCamera.Priority = cameraOwnerPriority;
            cinemachineCamera.Prioritize();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            OnPlayerDespawned?.Invoke(this);
        }
    }
}
