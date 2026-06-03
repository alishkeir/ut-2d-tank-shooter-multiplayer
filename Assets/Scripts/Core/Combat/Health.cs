using System;
using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [field: SerializeField] public int MaxHealth { get; private set; } = 100;

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>();

    private bool isDead = false;

    public Action<Health> OnDie;


    public override void OnNetworkDespawn()
    {
        if (!IsServer) { return; }

        CurrentHealth.Value = MaxHealth;
    }


    public void TakeDamage(int damage)
    {
        if (!IsServer) { return; }

        ModifyHealth(-damage);
    }


    public void RestoreHealth(int heal)
    {
        if (!IsServer) { return; }

        ModifyHealth(heal);

    }


    private void ModifyHealth(int value)
    {
        if (isDead) { return; }
        // if (!IsServer) { return; }

        int newHealth = CurrentHealth.Value + value;
        CurrentHealth.Value += Mathf.Clamp(newHealth, 0, MaxHealth);

        if (CurrentHealth.Value == 0)
        {
            OnDie?.Invoke(this);
            isDead = true;
        }
    }

}
