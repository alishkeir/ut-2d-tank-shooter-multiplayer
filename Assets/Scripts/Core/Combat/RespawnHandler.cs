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
    [SerializeField] private NetworkObject playerPrefab; // The player prefab to instantiate when respawning.

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        // Catch up on any players that already exist (e.g. late join).
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player player in players)
        {
            HandlePlayerSpawned(player);
        }

        // Subscribe to future spawn/despawn events.
        Player.OnPlayerSpawned += HandlePlayerSpawned;
        Player.OnPlayerDespawned += HandlePlayerDespawned;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }
        // NOTE: Original code unsubscribed from OnPlayerDespawned for the spawn callback.
        // This is preserved as-is to avoid changing runtime behavior; see source for details.
        Player.OnPlayerDespawned -= HandlePlayerSpawned;
        Player.OnPlayerDespawned -= HandlePlayerDespawned;

    }

    // Subscribe this player's death event so we can respawn them.
    private void HandlePlayerSpawned(Player player)
    {
        player.Health.OnDie += (health) => HandlePlayerDie(player);
    }


    // Unsubscribe from the player's death event when they despawn.
    private void HandlePlayerDespawned(Player player)
    {
        player.Health.OnDie -= (health) => HandlePlayerDie(player);

    }

    // Called when a player's Health reaches 0.
    // Destroys the old player object and starts the respawn coroutine.
    private void HandlePlayerDie(Player player)
    {
        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayerCoroutine(player.OwnerClientId));


    }

    // Waits one frame (so the destroy is fully processed) and then spawns a
    // fresh player object at a random spawn point, re-attaching it to the
    // original owner client.
    IEnumerator RespawnPlayerCoroutine(ulong ownerClientId)
    {
        yield return null;

        NetworkObject playerInstance = Instantiate(playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);
        playerInstance.SpawnAsPlayerObject(ownerClientId);

    }


}
