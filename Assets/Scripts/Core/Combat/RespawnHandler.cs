// RespawnHandler
// --------------
// Server-side logic that respawns players after they die.
//
// Flow:
// 1. On the server, this subscribes to Player.OnPlayerSpawned.
// 2. Whenever a player spawns, it subscribes to that player's Health.OnDie event.
// 3. When the player dies, the existing GameObject is destroyed and a coroutine
//    respawns the player at a random SpawnPoint (still owned by the same client).
//
// Place this on a NetworkObject in the game scene. Assign `playerPrefab` in
// the inspector to the same prefab used by the network player prefab list.
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private TankPlayer playerPrefab; // The player prefab to instantiate when respawning.
    [SerializeField] private float keptCoinsPercentage = 70f; // The percentage of coins the player keeps on death.

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        // Catch up on any players that already exist (e.g. late join).
        TankPlayer[] players = FindObjectsByType<TankPlayer>(FindObjectsSortMode.None);

        foreach (TankPlayer player in players)
        {
            HandlePlayerSpawned(player);
        }

        // Subscribe to future spawn/despawn events.
        TankPlayer.OnPlayerSpawned += HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned += HandlePlayerDespawned;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }
        // NOTE: Original code unsubscribed from OnPlayerDespawned for the spawn callback.
        // This is preserved as-is to avoid changing runtime behavior; see source for details.
        TankPlayer.OnPlayerDespawned -= HandlePlayerSpawned;
        TankPlayer.OnPlayerDespawned -= HandlePlayerDespawned;

    }

    // Subscribe this player's death event so we can respawn them.
    private void HandlePlayerSpawned(TankPlayer player)
    {
        player.Health.OnDie += (health) => HandlePlayerDie(player);
    }


    // Unsubscribe from the player's death event when they despawn.
    private void HandlePlayerDespawned(TankPlayer player)
    {
        player.Health.OnDie -= (health) => HandlePlayerDie(player);

    }

    // Called when a player's Health reaches 0.
    // Destroys the old player object and starts the respawn coroutine.
    // The coins are kept by the respawned player.
    private void HandlePlayerDie(TankPlayer player)
    {
        int coinsToKeep = (int)(player.Coins.TotalCoins.Value * (keptCoinsPercentage / 100f));

        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayerCoroutine(player.OwnerClientId, coinsToKeep));
    }

    // Waits one frame (so the destroy is fully processed) and then spawns a
    // fresh player object at a random spawn point, re-attaching it to the
    // original owner client.
    // The coins are kept by the respawned player.
    IEnumerator RespawnPlayerCoroutine(ulong ownerClientId, int coinsToKeep)
    {
        yield return null;

        TankPlayer playerInstance = Instantiate(playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);
        playerInstance.NetworkObject.SpawnAsPlayerObject(ownerClientId);
        playerInstance.Coins.TotalCoins.Value = coinsToKeep; // Keep the coins on respawn.

    }


}
