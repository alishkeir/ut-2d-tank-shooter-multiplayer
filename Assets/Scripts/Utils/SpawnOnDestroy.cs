// SpawnOnDestroy
// --------------
// Spawns a prefab at this object's position when it gets destroyed. Useful
// for things like death VFX, loot drops, or debris that should appear
// wherever the object was last seen.
//
// Setup:
// - Attach to any GameObject.
// - Assign `prefab` in the inspector to the prefab you want to spawn.
//
// The prefab is instantiated with the same position and identity rotation,
// so it appears exactly where this object was. There is no parenting.
using UnityEngine;

public class SpawnOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject prefab; // The prefab to instantiate when this object is destroyed.

    // Called by Unity when the GameObject is being destroyed (or right before the scene unloads).
    private void OnDestroy()
    {
        // Drop a fresh copy of the prefab at our last known position.
        Instantiate(prefab, transform.position, Quaternion.identity);
    }
}
