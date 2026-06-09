// ClientNetworkTransform
// ----------------------
// A subclass of Unity Netcode's NetworkTransform that makes the OWNING client
// the authority for the transform. This is the opposite of the default
// (server-authoritative) and is what allows responsive player movement:
// the local client moves the transform and the change is replicated to
// the server + other clients.
//
// Used by the player prefab (replace the default NetworkTransform with this
// one). It overrides:
// - OnIsServerAuthoritative() -> false (the owner is the authority).
// - OnNetworkDespawn()       -> resets CanCommitToTransform based on ownership.
using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform
{


    // Called by Netcode when this object is despawned. We use it to flip
    // CanCommitToTransform back to the ownership state in case the object
    // is respawned later under a different owner.
    // To know if we own the object, we need to override the OnNetworkSpawn method.
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        CanCommitToTransform = IsOwner;
    }


    // We don't want the server to be authoritative on the client for moving the player.
    // We override the OnIsServerAuthoritative method to return false, which makes
    // the owning client the authority.
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
