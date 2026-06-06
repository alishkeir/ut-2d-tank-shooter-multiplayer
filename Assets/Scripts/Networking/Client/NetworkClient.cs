
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkClient : IDisposable
{

    private const string _MENU_SCENE_NAME = "Menu";

    private NetworkManager networkManager;

    public NetworkClient(NetworkManager networkManager)
    {
        this.networkManager = networkManager;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }


    public void Dispose()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

        }
    }






    private void OnClientDisconnect(ulong clientId)
    {
        if (clientId != 0 && clientId != networkManager.LocalClientId) { return; }

        if (SceneManager.GetActiveScene().name != _MENU_SCENE_NAME)
        {
            SceneManager.LoadScene(_MENU_SCENE_NAME);
        }

        if (networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();
        }

    }



}
