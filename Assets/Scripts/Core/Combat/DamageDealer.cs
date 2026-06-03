using Unity.Netcode;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private int damage = 5;

    private ulong ownerClientId;

    public void SetOwner(ulong ownerClientId)

    {
        this.ownerClientId = ownerClientId;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {

        if (other.attachedRigidbody == null) { return; }



        if (other.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            // if (netObj.IsOwner) { return; }
            if (ownerClientId == netObj.OwnerClientId) { return; }

        }

        if (other.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(damage);

        }
    }
}
