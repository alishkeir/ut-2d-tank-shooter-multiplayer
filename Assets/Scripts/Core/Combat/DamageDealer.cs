// DamageDealer
// ------------
// Attach this to any projectile (or hazard) that should damage a Health component
// on contact. The owner (client who fired it) is tracked so a projectile never
// damages the player that shot it.
//
// Usage:
// - Add this component to your projectile prefab.
// - Call SetOwner(OwnerClientId) right after spawning the projectile so the
//   "self damage" check works.
// - The projectile's collider must be a 2D trigger.
//
// Behavior:
// - On any 2D trigger entry, if the colliding body has a Health component
//   AND its owner is different from this projectile's owner, damage is applied.
using Unity.Netcode;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private int damage = 5; // Damage applied to any valid Health target on contact.

    private ulong ownerClientId; // The client that fired this projectile (used to prevent self-damage).


    // Setter used by ProjectileLauncher (or any other system) to record the
    // firing client so this projectile can avoid damaging its owner.
    public void SetOwner(ulong ownerClientId)

    {
        this.ownerClientId = ownerClientId;
    }


    // Called automatically by Unity when this trigger collider overlaps another collider.
    private void OnTriggerEnter2D(Collider2D other)
    {

        // Ignore collisions that don't have a rigidbody (the body's "owner").
        if (other.attachedRigidbody == null) { return; }



        // If the other object is a NetworkObject owned by the same client, ignore it.
        if (other.attachedRigidbody.TryGetComponent<NetworkObject>(out NetworkObject netObj))
        {
            // if (netObj.IsOwner) { return; }
            if (ownerClientId == netObj.OwnerClientId) { return; }

        }

        // If the other object has a Health component, deal damage to it.
        if (other.attachedRigidbody.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(damage);

        }
    }
}
