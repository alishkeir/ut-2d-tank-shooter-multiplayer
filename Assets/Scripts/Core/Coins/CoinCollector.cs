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
using Unity.Netcode;
using UnityEngine;

public class CoinCollector : NetworkBehaviour
{
    // The total number of coins this collector has. Synced across the network.
    public NetworkVariable<int> TotalCoins = new NetworkVariable<int>();



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
}
