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
    [SerializeField] private int maxHealPower = 30;  // How many times can it restore health
    [SerializeField] private float healCooldown = 30f;   // How long to wait between healing.
    [SerializeField] private float healTickRate = 1f;   // How often to apply healing while active.
    [SerializeField] private int coinsPerTick = 10;     // How many coins to heal per tick.
    [SerializeField] private int healPerTick = 10;  // How much health to heal per tick.


    private float remainingCooldown;  // Remaining cooldown time.
    private float tickTimer;  // Remaining tick rate.
    private List<TankPlayer> playersInZone = new();  // List of players currently in the healing zone.

    private NetworkVariable<int> healPower = new();  // Current heal power, synchronized across the network.


    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            healPower.OnValueChanged += HandleHealPowerChanged;  // Subscribe to changes in heal power to update the UI.
            HandleHealPowerChanged(0, healPower.Value);  // Initialize the UI with the current heal power value.
        }

        if (IsServer)
        {
            healPower.Value = maxHealPower;  // Initialize heal power on the server.
        }
    }


    public override void OnNetworkDespawn()
    {
        if (IsClient)
        {
            healPower.OnValueChanged -= HandleHealPowerChanged;  // Unsubscribe from changes in heal power to update the UI.
        }
    }



    // Server-only setup: subscribe to the HealingZone's OnTriggerEnter2D event.
    // This is used to detect when a player enters the healing zone.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) { return; }
        if (!other.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) { return; }
        playersInZone.Add(player);
    }


    private void Update()
    {
        if (!IsServer) { return; }

        if (remainingCooldown > 0f)
        {
            remainingCooldown -= Time.deltaTime;  // Decrement the cooldown timer.

            if (remainingCooldown <= 0f)
            {
                healPower.Value = maxHealPower;  // Reset heal power when cooldown ends.
            }

            else
            {
                return;  // Cooldown not active, do nothing.
            }

        }

        tickTimer += Time.deltaTime;  // Increment the tick timer.
        if (tickTimer >= 1 / healTickRate) // ticks half the rate
        {

            // Apply healing to all players in the healing zone.
            foreach (TankPlayer player in playersInZone)
            {
                if (healPower.Value == 0) { break; }
                if (player.Health.CurrentHealth.Value >= player.Health.MaxHealth) { continue; }
                if (player.Coins.TotalCoins.Value < coinsPerTick) { continue; }

                player.Coins.SpendCoins(coinsPerTick);
                player.Health.RestoreHealth(healPerTick);
                healPower.Value -= 1;

                if (healPower.Value == 0) { remainingCooldown = healCooldown; }
            }
            tickTimer %= 1 / healTickRate;
        }
    }


    // Server-only cleanup: unsubscribe from the HealingZone's OnTriggerEnter2D event.
    // This is used to detect when a player leaves the healing zone.
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsServer) { return; }
        if (!other.attachedRigidbody.TryGetComponent<TankPlayer>(out TankPlayer player)) { return; }
        playersInZone.Remove(player);

    }

    // 
    private void HandleHealPowerChanged(int oldHealPower, int newHealPower)
    {
        healPowerBar.fillAmount = (float)newHealPower / maxHealPower;
    }
}
