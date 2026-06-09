// HealthDisplay
// -------------
// Client-side UI that visualizes a Health component's current value as a
// horizontal health bar. Attach this to any GameObject that has a UI Image
// (set to Filled) and assign a Health reference in the inspector.
//
// Behavior:
// - On client spawn, subscribes to CurrentHealth.OnValueChanged and immediately
//   sets the bar to the current value (so the bar shows correct data even if
//   health changed before this client connected).
// - On despawn, unsubscribes to avoid leaks.
// - The fill amount is updated as a 0..1 value (currentHealth / maxHealth).
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealthDisplay : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private Health health;            // The Health component to visualize.
    [SerializeField] private Image healthBarImage;    // The UI Image (set to Filled, Horizontal) that represents the bar.

    // Subscribe to health changes as soon as this object is spawned on a client.
    public override void OnNetworkSpawn()
    {
        if (!IsClient) { return; }

        health.CurrentHealth.OnValueChanged += HandleHealthChange;
        // Initialize the bar to the current value (in case health changed before we spawned).
        HandleHealthChange(0, health.CurrentHealth.Value);

    }
    // Unsubscribe on despawn to prevent memory leaks / missing references.
    public override void OnNetworkDespawn()
    {
        health.CurrentHealth.OnValueChanged -= HandleHealthChange;

    }


    // Update the visual fill amount whenever the server changes CurrentHealth.
    private void HandleHealthChange(int oldHealth, int newHealth)
    {
        healthBarImage.fillAmount = (float)newHealth / health.MaxHealth;
    }
}
