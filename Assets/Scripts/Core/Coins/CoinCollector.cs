// CoinCollector
// -------------
// Attach this to any NetworkObject (typically the player) that should be able
// to pick up coins. When a 2D trigger collision happens with a Coin object,
// the coin's Collect() method is called. If this is the server, the value
// returned by the coin is added to TotalCoins (a NetworkVariable<int> that
// is automatically synced to all clients).
//
// SpendCoins() lets other systems (e.g. ProjectileLauncher) deduct coins when
// the player spends them.
using System;
using Unity.Netcode;
using UnityEngine;

public class CoinCollector : NetworkBehaviour
{


    [Header("References")]
    [SerializeField] private BountyCoin bountyCoinPrefab;    // BountyCoin prefab to spawn.
    [SerializeField] private Health playerHealth; // Reference to the player's health component, used to determine when to spawn a bounty coin.

    [Header("Settings")]
    [SerializeField] private int bountyCoinCount = 10;        // How many bounty coins to spawn.
    [SerializeField] private float minBountyCoinValue = 10f;       // Minimum value of a bounty coin.
    [SerializeField] private float coinSpread = 3f;           // Coins Spread in a radius around the player when spawning.
    [SerializeField] private float bountyPercentage = 60f;   // Percentage of coins that are bounty coins.
    [SerializeField] private LayerMask layerMask;            // Layers to avoid (walls, players, etc.).




    public NetworkVariable<int> TotalCoins = new();        // The total number of coins this collector has. Synced across the network.

    private float coinRadius;        // Cached radius of the coin's collider, used for overlap checks.





    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        coinRadius = bountyCoinPrefab.GetComponent<CircleCollider2D>().radius;
        playerHealth.OnDie += HandleDie;
    }

    override public void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        playerHealth.OnDie -= HandleDie;
    }

    // Spawns bounty coins when the player dies, based on the current TotalCoins and the configured percentage and minimum value.
    private void HandleDie(Health health)
    {
        int bountyValue = (int)(TotalCoins.Value * bountyPercentage / 100f);
        int bountyCoinValue = bountyValue / bountyCoinCount;

        if (bountyCoinValue < minBountyCoinValue) { return; }

        for (int i = 0; i < bountyCoinCount; i++)
        {
            BountyCoin coinInstance = Instantiate(bountyCoinPrefab, GetSpawnPoint(), Quaternion.identity, transform);
            coinInstance.SetValue(bountyCoinValue);
            coinInstance.NetworkObject.Spawn();
        }
    }

    // Detect a 2D trigger collision with a coin pickup.
    private void OnTriggerEnter2D(Collider2D other)
    {

        // Ignore anything that isn't a Coin.
        if (!other.TryGetComponent<Coin>(out Coin coin)) { return; }

        // Ask the coin for its value (and have it run its collection logic).
        int coinValue = coin.Collect();

        // Only the server is allowed to mutate shared state.
        if (!IsServer) { return; }

        // Add the coin's value to this collector's running total.
        TotalCoins.Value += coinValue;

    }

    // Deducts coins from the total (e.g. when firing a projectile).
    // Safe to call on the server only - TotalCoins is a server-authoritative NetworkVariable.
    public void SpendCoins(int coins)
    {
        TotalCoins.Value -= coins;
    }

    // Picks a random point within the configured rectangle that does NOT overlap
    // anything on `layerMask`. Loops until a clear point is found.
    private Vector2 GetSpawnPoint()
    {
        while (true)
        {
            Vector2 spawnPoint = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * coinSpread;

            // Check if the chosen point overlaps any forbidden colliders.
            Collider2D hitCollider = Physics2D.OverlapCircle(spawnPoint, coinRadius, layerMask);

            if (hitCollider == null)
            {
                return spawnPoint; // No colliders found - this is a safe spawn point.
            }
        }
    }
}
