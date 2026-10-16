// ClientGameManager
// -----------------
// Non-MonoBehaviour class that owns the client-side networking logic for a
// multiplayer session. Instantiated by ClientSingleton on startup and disposed
// on shutdown.
//
// Responsibilities:
// - Initialize Unity Services and authenticate the local player.
// - Join a Relay allocation by code (StartClientAsync) and connect the
//   NetworkManager to the relay server.
// - Attach a UserData payload (username + Unity Auth ID) to the connection
//   so the server can identify us.
// - Track the joined lobby ID so we can remove ourselves from the lobby on
//   disconnect (LeaveLobby).
//
// Used by: ClientSingleton, MainMenu, LobbiesList.
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Lobbies;
using UnityEngine.SceneManagement;
using UnityEngine;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models; // UNITY 6 COMPAT: AllocationUtils moved here from Unity.Networking.Transport.Relay
using System.Text;
using Unity.Services.Authentication;

public class ClientGameManager : IDisposable
{
    private const string MenuSceneName = "MainMenu";

    private NetworkClient networkClient;

    private JoinAllocation allocation;

    private string joinedLobbyId;        // The lobby ID we are currently a member of (or null).
    private bool isStartingClient = false;   // True while we are starting the client.

    public async Task<bool> InitAsync()
    {
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(networkManager: NetworkManager.Singleton);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        return AuthenticationService.Instance.IsSignedIn;
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene(MenuSceneName);
    }

    public async Task StartClientAsync(string joinCode)
    {

        if (isStartingClient) { return; }
        isStartingClient = true;

        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);
        }
        catch (Exception exc)
        {
            Debug.LogError(exc);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);

        UserData userData = new UserData
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Missing Name"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        NetworkManager.Singleton.StartClient();

        isStartingClient = false;
    }

    // Stores the lobby ID after we join one. Used by LeaveLobby() during shutdown.
    public void SetJoinedLobbyId(string lobbyId)
    {
        joinedLobbyId = lobbyId;
    }


    // Removes us from the lobby we joined, if any. Best-effort; logs errors but does not throw.
    private async void LeaveLobby()
    {
        if (string.IsNullOrEmpty(joinedLobbyId)) { return; }

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(joinedLobbyId, AuthenticationService.Instance.PlayerId);
            joinedLobbyId = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to leave lobby: {e}");
        }
    }


    public void Disconnect()
    {
        networkClient.Disconnect();
    }

    // Called by ClientSingleton.OnDestroy. Leaves any joined lobby and disposes the NetworkClient.
    public void Dispose()
    {
        LeaveLobby();
        networkClient?.Dispose();
    }


}