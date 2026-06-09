using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealingZone : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Image healPowerBar;           // The power bar to fill up.
    [SerializeField] private LayerMask playerLayerMask;    // Layer mask to identify player objects for healing.

    [Header("Settings")]
    [SerializeField] private float maxHealPower = 30f;  // How many times can it restore health
    [SerializeField] private float healCooldown = 30f;   // How long to wait between healing.
    [SerializeField] private float healTickRate = 1f;   // How often to apply healing while active.
    [SerializeField] private int coinsPerTick = 10;     // How many coins to heal per tick.
    [SerializeField] private float healPerTick = 10f;  // How much health to heal per tick.

    private List<Player> playersInZone = new();  // List of players currently in the healing zone.


    // Server-only setup: subscribe to the HealingZone's OnTriggerEnter2D event.
    // This is used to detect when a player enters the healing zone.
    private void OnTriggerEnter2D(Collider2D other)
    {

        if (!IsServer) { return; }
        if (!other.attachedRigidbody.TryGetComponent<Player>(out Player player)) { return; }
        playersInZone.Add(player);

        Debug.Log($"Player {player.PlayerName.Value} entered healing zone.");


    }


    // Server-only cleanup: unsubscribe from the HealingZone's OnTriggerEnter2D event.
    // This is used to detect when a player leaves the healing zone.
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) { return; }
        if (!other.attachedRigidbody.TryGetComponent<Player>(out Player player)) { return; }
        playersInZone.Remove(player);
        Debug.Log($"Player {player.PlayerName.Value} left healing zone.");

    }
}
