// CoinSpawner
// -----------
// Server-authoritative spawner for RespawningCoin pickups.
//
// On the server, when this NetworkObject spawns, it:
// 1. Creates a parent NetworkObject to keep the hierarchy clean.
// 2. Spawns `maxCoins` coins at random positions in the configured rectangle
//    (avoiding any colliders on `layerMask`).
// 3. Subscribes to each coin's OnCollected event so it can respawn the coin
//    at a new random location after it is picked up.
//
// Configuration (inspector):
// - coinPrefab:        The RespawningCoin prefab to spawn.
// - maxCoins:          How many coins to keep alive in the level.
// - coinValue:         How much each coin is worth.
// - xSpawnRange/ySpawnRange: The (min, max) rectangle to spawn coins in.
// - layerMask:         Layers to avoid (e.g. walls, players) when picking a spawn point.
// - coinParentPrefab:  An empty NetworkObject prefab to use as a parent in the hierarchy.
using Unity.Netcode;
using UnityEngine;

public class CoinSpawner : NetworkBehaviour
{
    [SerializeField] private RespawningCoin coinPrefab;      // The coin prefab to spawn.
    [SerializeField] private int maxCoins = 50;              // Total number of coins to keep active.
    [SerializeField] private int coinValue = 10;             // Value assigned to every spawned coin.

    [SerializeField] private Vector2 xSpawnRange;            // (min, max) X range for spawning.
    [SerializeField] private Vector2 ySpawnRange;            // (min, max) Y range for spawning.

    [SerializeField] private LayerMask layerMask;            // Layers to avoid (walls, players, etc.).

    [SerializeField] private NetworkObject coinParentPrefab; // Empty NetworkObject prefab to parent coins under.



    private float coinRadius;        // Cached radius of the coin's collider, used for overlap checks.
    private Transform coinParent;    // Runtime parent transform for all spawned coins.



    // Server-only setup: create the parent and spawn the initial set of coins.
    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        // Instantiate and spawn the parent NetworkObject so all coins are visible
        // to clients in the hierarchy.
        var parentObj = Instantiate(coinParentPrefab);
        parentObj.Spawn();

        coinParent = parentObj.transform;

        // Cache the coin's collider radius for overlap checks.
        coinRadius = coinPrefab.GetComponent<CircleCollider2D>().radius;

        // Pre-populate the level with the configured number of coins.
        for (int i = 0; i < maxCoins; i++)
        {
            SpawnCoin();
        }
    }


    // Instantiates a single coin at a valid spawn point and spawns it on the network.
    private void SpawnCoin()
    {
        RespawningCoin coinInstance = Instantiate(coinPrefab, GetSpawnPoint(), Quaternion.identity, coinParent);

        coinInstance.SetValue(coinValue);
        coinInstance.GetComponent<NetworkObject>().Spawn();
        coinInstance.OnCollected += HandleCoinCollected;
        coinInstance.transform.SetParent(coinParent);

        Debug.Log(coinParent.name);
    }

    // Called whenever a coin is collected. Re-spawns it at a new valid location.
    private void HandleCoinCollected(RespawningCoin coin)
    {
        coin.transform.position = GetSpawnPoint();
        coin.Reset();
    }


    // Picks a random point within the configured rectangle that does NOT overlap
    // anything on `layerMask`. Loops until a clear point is found.
    private Vector2 GetSpawnPoint()
    {
        float x = 0f;
        float y = 0f;

        while (true)
        {
            x = Random.Range(xSpawnRange.x, xSpawnRange.y);
            y = Random.Range(xSpawnRange.x, ySpawnRange.y);

            Vector2 spawnPoint = new Vector2(x, y);

            // Check if the chosen point overlaps any forbidden colliders.
            Collider2D hitCollider = Physics2D.OverlapCircle(spawnPoint, coinRadius, layerMask);

            if (hitCollider == null)
            {
                return spawnPoint; // No colliders found - this is a safe spawn point.
            }
        }
    }
}
