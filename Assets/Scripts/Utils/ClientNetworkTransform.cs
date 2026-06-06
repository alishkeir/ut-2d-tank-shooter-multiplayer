using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform
{


    // to know if we own the object, we need to override the OnNetworkSpawn method
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        CanCommitToTransform = IsOwner;
    }


    // we don't want to be authoritative on the client to move the player
    // so we override the OnIsServerAuthoritative method
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
