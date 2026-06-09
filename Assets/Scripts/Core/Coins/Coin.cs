// Coin (abstract base class)
// --------------------------
// Networked base class for any collectible coin in the game.
//
// Provides shared behavior:
// - A configurable value (coinValue) and an "already collected" flag.
// - A Show/Hide helper that toggles the SpriteRenderer (so subclasses can hide
//   the coin visually after pickup without disabling the GameObject, which
//   would interfere with NetworkObject despawn/spawn timing).
// - An abstract Collect() method that subclasses must implement.
//
// Subclasses (e.g. RespawningCoin) define the actual collect behavior.
using Unity.Netcode;
using UnityEngine;

public abstract class Coin : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer; // The visual representation of the coin.

    protected int coinValue = 2;       // How many coins this pickup is worth.
    protected bool alraedyCollected;   // Note: typo preserved from original ("alraedy"). Tracks if the coin has already been picked up.

    // Subclasses implement the actual collection logic.
    // Returns the amount of currency the picker should receive.
    public abstract int Collect();

    // Lets spawners configure how much this coin is worth.
    public void SetValue(int value)
    {
        coinValue = value;
    }

    // Toggles the coin's visibility (used so the coin "disappears" briefly
    // after collection, even though the GameObject stays alive for networking).
    protected void Show(bool show)
    {
        spriteRenderer.enabled = show;
    }

}
