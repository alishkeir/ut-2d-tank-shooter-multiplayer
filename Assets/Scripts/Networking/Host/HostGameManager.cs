// HostGameManager
// ---------------
// Non-MonoBehaviour class that owns the host-side networking logic for a
// multiplayer session. Instantiated by HostSingleton on startup and disposed
// on shutdown.
//
// Responsibilities:
// - Create a Relay allocation and obtain a join code (StartHostAsync).
// - Create a Unity Lobby so other clients can find and join us via the lobby list.
// - Spin up a NetworkServer to track connected users and broadcast disconnects.
// - Attach a UserData payload (our username + Unity Auth ID) to the host connection.
// - Start the Netcode host and load the Game scene.
//
// Used by: HostSingleton, MainMenu (via HostSingleton.Instance.GameManager).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostGameManager : IDisposable
{
    // Max number of players that can connect to a single host (Relay + Lobby limit).
    private const int _MAX_CONNECTIONS = 20;
    // Name of the scene the host loads after starting. Must match the scene in Build Settings.
    private const string _GAME_SCENE_NAME = "Game";

    private Allocation allocation; // Our Relay allocation.
    private string joinCode;       // The Relay join code other clients use to connect.

    private Lobby lobby;           // The Unity Lobby we created for matchmaking.


    // The NetworkServer that tracks connected clients and approves connections.
    public NetworkServer NetworkServer { get; private set; }


    // Cleanly tears down the host: deletes the lobby, disposes the NetworkServer.
    public void Dispose()
    {
        try
        {
            if (lobby != null)
            {
                // Best-effort: ask the lobby service to remove our lobby.
                LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }

        lobby = null;
        NetworkServer?.Dispose();
    }


    // Starts the host end-to-end:
    // 1. Creates a Relay allocation (up to _MAX_CONNECTIONS).
    // 2. Asks the Relay service for a join code.
    // 3. Configures Unity Transport to use the Relay server.
    // 4. Creates a public Unity Lobby with the join code in its data.
    // 5. Spins up the NetworkServer to track users.
    // 6. Attaches our UserData payload to the connection.
    // 7. Starts the Netcode host and loads the Game scene.
    public async Task StartHostAsync()
    {
        try
        {
            // Ask the Relay service to allocate a slot for up to _MAX_CONNECTIONS clients.
            allocation = await RelayService.Instance.CreateAllocationAsync(_MAX_CONNECTIONS);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        try
        {
            // Convert the allocation into a short join code other clients can enter.
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Join Code: {joinCode}");
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

        try
        {
            // Create a lobby that's discoverable in the lobby list (IsPrivate = false).
            // The join code is stored in the lobby data so clients can read it on join.
            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                }
            };

            string playerName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player");

            // The lobby name is "<playerName>'s Lobby".
            lobby = await LobbyService.Instance.CreateLobbyAsync($"{playerName}'s Lobby", _MAX_CONNECTIONS, lobbyOptions);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return;
        }

        NetworkServer = new NetworkServer(NetworkManager.Singleton);

        // Remove players from the lobby when they disconnect, so the player count stays accurate.
        NetworkServer.OnUserLeft += OnUserLeft;

        // Build a UserData payload with our display name + Unity Auth ID.
        // The server reads it via NetworkServer.ApprovalCheck.
        UserData userData = new()
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        // Start the Netcode host (server + local client) and load the game scene.
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(_GAME_SCENE_NAME, LoadSceneMode.Single);
    }

    // Called by NetworkServer when a connected user disconnects.
    // Removes that user from the lobby so other clients see an updated player count.
    private async void OnUserLeft(string authId)
    {
        if (lobby == null) { return; }

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobby.Id, authId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to remove player from lobby: {e}");
        }
    }

    // Cleanly shuts down the host: stops the NetworkManager, deletes the lobby,
    // and disposes the NetworkServer. Safe to call multiple times.
    public async Task ShutdownAsync()
    {
        if (NetworkServer != null)
        {
            NetworkServer.OnUserLeft -= OnUserLeft;
        }

        try
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        try
        {
            if (lobby != null)
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
                lobby = null;
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }

        NetworkServer?.Dispose();
        NetworkServer = null;
    }
}
