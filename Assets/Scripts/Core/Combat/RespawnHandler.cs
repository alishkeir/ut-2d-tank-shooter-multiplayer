using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player player in players)
        {
            HandlePlayerSpawned(player);
        }

        Player.OnPlayerSpawned += HandlePlayerSpawned;
        Player.OnPlayerDespawned += HandlePlayerDespawned;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }
        Player.OnPlayerDespawned -= HandlePlayerSpawned;
        Player.OnPlayerDespawned -= HandlePlayerDespawned;

    }

    private void HandlePlayerSpawned(Player player)
    {
        player.Health.OnDie += (health) => HandlePlayerDie(player);
    }


    private void HandlePlayerDespawned(Player player)
    {
        player.Health.OnDie -= (health) => HandlePlayerDie(player);

    }

    private void HandlePlayerDie(Player player)
    {
        Destroy(player.gameObject);

        StartCoroutine(RespawnPlayerCoroutine(player.OwnerClientId));


    }

    IEnumerator RespawnPlayerCoroutine(ulong ownerClientId)
    {
        yield return null;

        NetworkObject playerInstance = Instantiate(playerPrefab, SpawnPoint.GetRandomSpawnPos(), Quaternion.identity);
        playerInstance.SpawnAsPlayerObject(ownerClientId);

    }


}
