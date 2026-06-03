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
        Debug.Log("OnTriggerEnter2D");

        if (other.attachedRigidbody == null) { return; }

        Debug.Log("OnTriggerEnter2D 2");


        if (other.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            // if (netObj.IsOwner) { return; }
            Debug.Log("OnTriggerEnter2D 3");
            if (ownerClientId == netObj.OwnerClientId) { return; }
            Debug.Log("OnTriggerEnter2D 4");

        }

        if (other.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(damage);
            Debug.Log("OnTriggerEnter2D 5");

        }
    }
}
