using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealthDisplay : NetworkBehaviour
{

    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Image healthBarImage;

    public override void OnNetworkSpawn()
    {
        if (!IsClient) { return; }

        health.CurrentHealth.OnValueChanged += HandleHealthChange;
        HandleHealthChange(0, health.CurrentHealth.Value);

    }
    public override void OnNetworkDespawn()
    {
        health.CurrentHealth.OnValueChanged -= HandleHealthChange;

    }


    private void HandleHealthChange(int oldHealth, int newHealth)
    {
        healthBarImage.fillAmount = (float)newHealth / health.MaxHealth;
    }
}
