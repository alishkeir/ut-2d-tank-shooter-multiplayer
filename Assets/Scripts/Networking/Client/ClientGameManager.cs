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
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ClientGameManager : IDisposable
{

    private NetworkClient networkClient; // Wraps the NetworkManager and handles disconnect events.
    private JoinAllocation allocation;   // Our allocation on the Relay server.
    private string joinedLobbyId;        // The lobby ID we are currently a member of (or null).
    private bool isStartingClient = false;   // True while we are starting the client.



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

    // One-time setup. Initializes Unity Services, creates a NetworkClient, and authenticates.
    // Returns true if the player is now Authenticated and ready to join a game.
    public async Task<bool> InitAsync()
    {
        // Initialize Unity Services (idempotent).
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(NetworkManager.Singleton);

        // Authenticate the player (anonymous sign-in).
        AuthState authState = await AuthenticationHandler.DoAuth();

        if (authState == AuthState.Authenticated)
        {
            return true;
        }
        return false;
    }

    // Joins a Relay allocation using the given join code, configures the
    // NetworkManager transport, attaches our UserData payload, and starts the client.
    public async Task StartClientAsync(string joinCode)
    {
        if (isStartingClient) { return; }
        isStartingClient = true;

        try
        {
            // Ask the Relay service to let us join the host's allocation.
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        // Configure Unity Transport to talk to the Relay server.
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

        transport.SetRelayServerData(relayServerData);

        // Build a UserData payload containing our display name and Unity Auth ID.
        // The server reads this in NetworkServer.ApprovalCheck to identify us.
        UserData userData = new()
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

        // Attach the payload to the connection. The server will read it on connect.
        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        // Finally, start the client and connect.
        NetworkManager.Singleton.StartClient();

        isStartingClient = false;
    }

    // Stores the lobby ID after we join one. Used by LeaveLobby() during shutdown.
    public void SetJoinedLobbyId(string lobbyId)
    {
        joinedLobbyId = lobbyId;
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
