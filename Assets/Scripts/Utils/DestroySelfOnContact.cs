// DestroySelfOnContact
// --------------------
// Tiny utility component: destroys the GameObject it is attached to as soon
// as it enters a 2D trigger collider. Use it on non-networked things like
// impact particles or sound-emitter prefabs that should disappear on the
// first contact with anything else.
using UnityEngine;

public class DestroySelfOnContact : MonoBehaviour
{
    // Called automatically by Unity on any 2D trigger entry.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Destroy this GameObject (note: the `gameObject` reference, not `this`).
        Destroy(gameObject);
    }
}
