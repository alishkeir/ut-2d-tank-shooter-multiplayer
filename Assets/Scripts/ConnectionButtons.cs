// ConnectionButtons
// -----------------
// A simple UI helper for starting a Netcode connection via the NetworkManager.
//
// Usage:
// - Attach this component to a GameObject in the scene (typically a button group).
// - Wire its public methods to UI Button OnClick events in the inspector:
//     * StartClient() -> connect to an existing host as a client.
//     * StartHost()    -> start both a server and a local client on this machine.
//
// This script does not handle authentication, lobby joining, or scene management.
// Those flows live in ClientSingleton/HostSingleton and the *GameManager classes.
using Unity.Netcode;
using UnityEngine;

public class ConnectionButtons : MonoBehaviour
{
    // Starts a Netcode client connection using the currently configured transport.
    // Requires a NetworkManager.Singleton to be present in the scene.
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    // Starts a Netcode host (server + local client) using the currently configured transport.
    // Requires a NetworkManager.Singleton to be present in the scene.
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }
}
