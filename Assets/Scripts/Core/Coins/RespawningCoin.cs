// RespawningCoin
// --------------
// A concrete Coin subclass that hides itself visually when picked up and then
// re-appears at a new spawn position (managed by CoinSpawner).
//
// Behavior:
// - On the client side (non-server), calling Collect() just hides the sprite.
// - On the server, Collect() marks the coin as collected, fires OnCollected
//   (so CoinSpawner can move it), and returns its value.
// - Reset() is called by CoinSpawner to mark the coin as collectible again
//   at its new position.
// - The Update() check re-shows the sprite whenever the coin's position
//   actually changes (i.e. when CoinSpawner teleports it to a new spot).
using System;
using UnityEngine;


public class RespawningCoin : Coin
{

    // Fired on the server when the coin is collected. CoinSpawner listens to
    // this so it can re-place the coin at a new random spawn point.
    public event Action<RespawningCoin> OnCollected;

    private Vector3 previousPosition; // Used to detect when the coin has been moved (and should be re-shown).


    private void Update()
    {

        // If the coin was moved (e.g. by CoinSpawner to a new position), make it visible again.
        if (previousPosition != transform.position)
        {
            Show(true);
        }

        previousPosition = transform.position;
    }

    public override int Collect()
    {
        // Clients just hide the visual; the server handles the actual game logic.
        if (!IsServer)
        {
            Show(false);
            return 0;
        }

        // Don't allow double-collection.
        if (alraedyCollected) { return 0; }

        alraedyCollected = true;

        // Notify CoinSpawner that this coin has been collected.
        OnCollected?.Invoke(this);

        return coinValue;
    }

    // Server-only: called by CoinSpawner after it has moved the coin to a new position.
    // Resets the "already collected" flag so the coin can be collected again.
    public void Reset()
    {
        alraedyCollected = false;
    }
}
