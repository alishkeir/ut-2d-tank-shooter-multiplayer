// Health
// ------
// A networked health component for any damageable entity (typically the player).
//
// Behavior:
// - On the server, when the NetworkObject spawns, CurrentHealth is initialized to MaxHealth.
// - TakeDamage(int) and RestoreHealth(int) adjust the value, clamped to [0, MaxHealth].
// - When CurrentHealth reaches 0, the OnDie event fires once and the entity
//   is marked as dead so further damage/heal calls are ignored.
//
// CurrentHealth is a NetworkVariable<int> and is automatically synced to all
// clients. Use HealthDisplay (or your own UI) to react to OnValueChanged.
using System;
using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100; // Maximum health value (clamped to this).

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();    // Current health, synced across the network.

    private bool isDead = false; // Set true the first time health hits 0; prevents double-death events.

    // Fired on the server when the entity's health drops to 0.
    // Listeners (e.g. RespawnHandler) should respond to this.
    public Action<Health> OnDie;


    // Initialize health to max on the server when this object spawns.
    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        CurrentHealth.Value = MaxHealth;
    }


    // Apply damage. Wraps ModifyHealth with a negative value.
    public void TakeDamage(int damage)
    {
        ModifyHealth(-damage);
    }


    // Heal the entity by the given amount. Wraps ModifyHealth with a positive value.
    public void RestoreHealth(int heal)
    {
        ModifyHealth(heal);
    }


    // The single point where CurrentHealth is changed.
    // - Ignored if the entity is already dead.
    // - Clamped between 0 and MaxHealth.
    // - Fires OnDie exactly once when health first reaches 0.
    private void ModifyHealth(int value)
    {
        if (isDead) { return; }
        // if (!IsServer) { return; }

        int newHealth = CurrentHealth.Value + value;
        CurrentHealth.Value = Mathf.Clamp(newHealth, 0, MaxHealth);

        if (CurrentHealth.Value == 0)
        {
            OnDie?.Invoke(this);
            isDead = true;
        }
    }

}
