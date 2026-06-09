// NetworkClient
// -------------
// A small wrapper around Unity Netcode's NetworkManager that reacts to
// client-side disconnects by returning the player to the main menu.
//
// Constructed by ClientGameManager (one per session). Subscribes to
// NetworkManager.OnClientDisconnectCallback; when we (or the local client)
// lose the connection, we tear down the NetworkManager and load the menu scene.
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkClient : IDisposable
{

    // Name of the main menu scene. Must match the scene in Build Settings.
    private const string _MENU_SCENE_NAME = "Menu";

    private NetworkManager networkManager; // The Netcode NetworkManager we are monitoring.


    // Subscribe to the disconnect event. Called from ClientGameManager.InitAsync.
    public NetworkClient(NetworkManager networkManager)
    {
        this.networkManager = networkManager;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }


    // Clean up our subscription. Called from ClientGameManager.Dispose.
    public void Dispose()
    {
        if (networkManager != null)
        {
            // Unsubscribe so we don't react to disconnect events after disposal.
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

        }
    }






    // Called by Netcode when a client disconnects. We only care about disconnects
    // for the local client (id 0 / LocalClientId) - remote disconnects are handled
    // by the server. When that happens we bounce the player back to the menu and
    // shut the NetworkManager down.
    private void OnClientDisconnect(ulong clientId)
    {
        if (clientId != 0 && clientId != networkManager.LocalClientId) { return; }

        // If we're not already in the menu, load it.
        if (SceneManager.GetActiveScene().name != _MENU_SCENE_NAME)
        {
            SceneManager.LoadScene(_MENU_SCENE_NAME);
        }

        // Make sure the NetworkManager is fully shut down.
        if (networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();
        }

    }



}
