// Leaderboard
// -----------
// Networked scoreboard that ranks players by their total coin count.
//
// How it works:
// - The server keeps a NetworkList<LeaderboardEntityState> that mirrors
//   the players currently in the match.
// - When a player spawns (Player.OnPlayerSpawned), the server adds an entry.
//   When a player despawns, the server removes it.
// - The server also subscribes to each player's CoinCollector.TotalCoins
//   changes and updates the matching entry's Coins value.
// - All clients subscribe to leaderboardEntities.OnListChanged and keep a
//   pool of LeaderboardEntityDisplay UI prefabs in sync (add / remove / update).
// - After every change, entries are sorted by coin count (descending) and
//   re-ordered in the hierarchy. Only the top N (`entitiesToDisplay`) are
//   shown, but the local player is always shown even if they're outside the top N.
//
// Place this on a NetworkObject in the game scene and assign the inspector
// fields (UI parent, entity prefab, etc.).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Leaderboard : NetworkBehaviour
{
    [SerializeField] private Transform leaderboardEntityHolder;     // Parent transform that holds the UI rows.
    [SerializeField] private LeaderboardEntityDisplay leaderboardEntityPrefab; // Prefab for a single row.
    [SerializeField] private int entitiesToDisplay = 8;             // How many rows to show at once.

    // Server-authoritative list of leaderboard entries, synced to all clients.
    private NetworkList<LeaderboardEntityState> leaderboardEntities;

    // Client-side cache of all spawned UI rows.
    private List<LeaderboardEntityDisplay> entityDisplays = new();



    private void Awake()
    {
        // NetworkList must be constructed in Awake (not in a field initializer).
        leaderboardEntities = new NetworkList<LeaderboardEntityState>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            // React to any list changes (add / remove / value update).
            leaderboardEntities.OnListChanged += HandleLeaderboardEntitiesChanged;
            // Replay any existing entries so we render the current state.
            foreach (LeaderboardEntityState entity in leaderboardEntities)
            {
                HandleLeaderboardEntitiesChanged(new NetworkListEvent<LeaderboardEntityState>
                {
                    Type = NetworkListEvent<LeaderboardEntityState>.EventType.Add,
                    Value = entity
                });
            }
        }

        if (IsServer)
        {
            // Catch up on players that already exist (e.g. late join).
            Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
            foreach (Player player in players)
            {
                HandlePlayerSpawned(player);
            }

            // Track future spawn / despawn events.
            Player.OnPlayerSpawned += HandlePlayerSpawned;
            Player.OnPlayerDespawned += HandlePlayerDespawned;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            leaderboardEntities.OnListChanged -= HandleLeaderboardEntitiesChanged;
        }

        if (IsServer)
        {
            Player.OnPlayerSpawned -= HandlePlayerSpawned;
            Player.OnPlayerDespawned -= HandlePlayerDespawned;
        }
    }

    // React to add/remove/value changes of the network list.
    private void HandleLeaderboardEntitiesChanged(NetworkListEvent<LeaderboardEntityState> changeEvent)
    {
        switch (changeEvent.Type)
        {
            case NetworkListEvent<LeaderboardEntityState>.EventType.Add:

                // Only create a UI row if we don't already have one for this client.
                if (!entityDisplays.Any(x => x.ClientId == changeEvent.Value.ClientId))
                {
                    LeaderboardEntityDisplay entityDisplay = Instantiate(leaderboardEntityPrefab, leaderboardEntityHolder);
                    entityDisplay.Initialise(changeEvent.Value.ClientId, changeEvent.Value.PlayerName, changeEvent.Value.Coins);
                    entityDisplays.Add(entityDisplay);
                }
                break;

            case NetworkListEvent<LeaderboardEntityState>.EventType.Remove:
                LeaderboardEntityDisplay entityDisplayToRemove = entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);
                if (entityDisplayToRemove != null)
                {
                    // Detach and destroy the UI row.
                    entityDisplayToRemove.transform.SetParent(null);
                    Destroy(entityDisplayToRemove.gameObject);
                    entityDisplays.Remove(entityDisplayToRemove);
                }
                break;

            case NetworkListEvent<LeaderboardEntityState>.EventType.Value:
                LeaderboardEntityDisplay entityDisplayToUpdate = entityDisplays.FirstOrDefault(x => x.ClientId == changeEvent.Value.ClientId);

                if (entityDisplayToUpdate != null)
                {
                    entityDisplayToUpdate.UpdateCoins(changeEvent.Value.Coins);
                }
                break;

        }

        // Sort all rows by coin count, descending (highest first).
        entityDisplays.Sort((x, y) => y.Coins.CompareTo(x.Coins));

        // Re-parent in the new order and refresh the text on every row.
        for (int i = 0; i < entityDisplays.Count; i++)
        {
            entityDisplays[i].transform.SetSiblingIndex(i);
            entityDisplays[i].UpdateText();
            bool shouldShow = i <= entitiesToDisplay - 1;
            entityDisplays[i].gameObject.SetActive(shouldShow);

            // Always show the local player even if they are outside the top N.
            LeaderboardEntityDisplay myDisplay = entityDisplays.FirstOrDefault(x => x.ClientId == NetworkManager.Singleton.LocalClientId);
            if (myDisplay != null)
            {
                if (myDisplay.transform.GetSiblingIndex() >= entitiesToDisplay)
                {
                    // Hide whoever is at the bottom of the visible list and show the local player instead.
                    leaderboardEntityHolder.GetChild(entitiesToDisplay - 1).gameObject.SetActive(false);
                    myDisplay.gameObject.SetActive(true);
                }
            }

        }
    }

    // Server-only: called when a player spawns. Adds them to the leaderboard
    // and subscribes to their coin-count changes.
    private void HandlePlayerSpawned(Player player)
    {
        leaderboardEntities.Add(new LeaderboardEntityState
        {
            ClientId = player.OwnerClientId,
            PlayerName = player.PlayerName.Value,
            Coins = 0
        });

        player.Coins.TotalCoins.OnValueChanged += (oldcoins, newCoins) => HandleCoinsChanged(player.OwnerClientId, newCoins);
    }

    // Server-only: called when a player despawns. Removes them from the leaderboard.
    private void HandlePlayerDespawned(Player player)
    {
        foreach (LeaderboardEntityState entity in leaderboardEntities)
        {
            if (entity.ClientId != player.OwnerClientId) { continue; }

            leaderboardEntities.Remove(entity);
            break;
        }

        player.Coins.TotalCoins.OnValueChanged -= (oldcoins, newCoins) => HandleCoinsChanged(player.OwnerClientId, newCoins);

    }

    // Server-only: when a player's coins change, find their leaderboard entry
    // and replace it with a new one that has the updated coin count.
    private void HandleCoinsChanged(ulong clientId, int newCoins)
    {

        for (int i = 0; i < leaderboardEntities.Count; i++)
        {
            if (leaderboardEntities[i].ClientId != clientId) { continue; }

            leaderboardEntities[i] = new()
            {
                ClientId = leaderboardEntities[i].ClientId,
                PlayerName = leaderboardEntities[i].PlayerName,
                Coins = newCoins
            };

            return;
        }



    }
}
