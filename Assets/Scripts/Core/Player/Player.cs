// Player
// ------
// Represents a single networked player in the match. Holds references to the
// player's Health, CoinCollector, and Cinemachine camera.
//
// Responsibilities:
// - On the server: assign the player's name from the authenticated UserData
//   and fire static OnPlayerSpawned/OnPlayerDespawned events (used by
//   RespawnHandler and the Leaderboard).
// - On the owner client: raise the Cinemachine camera's priority so the local
//   player controls their own follow camera (and not someone else's).
//
// PlayerName is a NetworkVariable<FixedString32Bytes> synced to all clients
// so every UI element (chat, leaderboard, nameplate) can display the same name.
using System;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera; // The follow camera for this player.

    [field: SerializeField] public Health Health { get; private set; }            // Health component on the player.
    [field: SerializeField] public CoinCollector Coins { get; private set; }     // Coin pickup component on the player.

    [SerializeField] private int cameraOwnerPriority = 20; // Priority value used to make the local player's camera "win" the Cinemachine blend.

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(); // a tring value that is synced over the network

    // Static events fired on the server when players spawn/despawn.
    // RespawnHandler and Leaderboard both subscribe to these.
    public static event Action<Player> OnPlayerSpawned;
    public static event Action<Player> OnPlayerDespawned;


    public override void OnNetworkSpawn()
    {

        if (IsServer)
        {
            // Look up the authenticated user's display name and broadcast it.
            UserData userData = HostSingleton.Instance.GameManager.NetworkServer.GetUserDataByClientId(OwnerClientId);
            PlayerName.Value = userData.userName;

            // Tell the rest of the server-side systems that this player is alive.
            OnPlayerSpawned?.Invoke(this);
        }

        if (IsOwner)
        {
            // The local player should be the one this camera follows.
            // Bumping the priority makes Cinemachine pick this camera.
            cinemachineCamera.Priority = cameraOwnerPriority;
            cinemachineCamera.Prioritize();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // Notify server-side listeners that this player is gone.
            OnPlayerDespawned?.Invoke(this);
        }
    }
}
