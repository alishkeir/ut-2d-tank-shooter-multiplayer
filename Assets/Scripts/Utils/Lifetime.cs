// Lifetime
// --------
// Self-destructs the GameObject it is attached to after a configurable number
// of seconds. Useful for things like projectile decals, temporary VFX, or
// non-networked explosions that should clean themselves up automatically.
//
// Setup:
// - Attach to any GameObject.
// - Set `lifetime` in the inspector to the number of seconds it should live.
using UnityEngine;

public class Lifetime : MonoBehaviour
{
    [SerializeField] float lifetime = 2f; // How long, in seconds, the object should live before being destroyed.

    private void Start()
    {
        // Schedule destruction `lifetime` seconds from now.
        Destroy(gameObject, lifetime);
    }
}
