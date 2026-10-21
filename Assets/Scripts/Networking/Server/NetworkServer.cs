// NetworkServer
// -------------
// Server-side helper that runs alongside Netcode for GameObjects.
//
// Responsibilities:
// - Approve (or reject) incoming connections, parse the UserData payload that
//   each client sends, and remember the mapping from ClientId -> AuthId -> UserData.
// - React to disconnects by raising OnUserLeft with the disconnected user's
//   AuthId (so the lobby can be kept in sync).
// - Provide a lookup so gameplay code (e.g. Player.OnNetworkSpawn) can resolve
//   a ClientId to a UserData (for setting the player's display name, etc.).
//
// Constructed by HostGameManager; do not create one yourself.
using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkServer : IDisposable
{

    private NetworkManager networkManager; // The Netcode NetworkManager we are wrapping.

    // Fired on the server when a connected user disconnects.
    // The argument is the user's AuthId (from UserData.userAuthId).
    public event Action<string> OnClientLeft;

    public event Action<UserData> OnUserJoined;
    public event Action<UserData> OnUserLeft;

    // Lookup tables: clientId <-> authId <-> UserData.
    private Dictionary<ulong, string> clientIdToAuth = new();
    private Dictionary<string, UserData> authIdToUserData = new();


    // Subscribes to connection approval and server-start events. Called from HostGameManager.
    public NetworkServer(NetworkManager networkManager)
    {
        this.networkManager = networkManager;

        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        networkManager.OnServerStarted += OnNetworkReady;

    }

    // Returns the UserData for a given ClientId, or null if unknown.
    // Used by Player.OnNetworkSpawn to get the player's display name.
    public UserData GetUserDataByClientId(ulong clientId)
    {
        if (clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            if (authIdToUserData.TryGetValue(authId, out UserData data))
            {
                return data;
            }

            return null;
        }

        return null;
    }



    public bool OpenConnection(string ip, int port)
    {
        UnityTransport transport = networkManager.gameObject.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, (ushort)port);
        return networkManager.StartServer();
    }


    // Called by Netcode for every incoming connection. We:
    // 1. Read the UserData payload the client attached to its connection.
    // 2. Remember the ClientId <-> AuthId <-> UserData mappings.
    // 3. Approve the connection and place the player at a random SpawnPoint.
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // A Relay session starts the dedicated server as a Host, so Netcode runs this callback for the
        // server's own local client with an empty payload. Approve it, but don't spawn a player for it.
        UserData userData = null;
        if (request.Payload != null && request.Payload.Length > 0)
        {
            try
            {
                string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
                userData = JsonUtility.FromJson<UserData>(payload);
            }
            catch (ArgumentException)
            {
                userData = null;
            }
        }

        if (userData == null || string.IsNullOrEmpty(userData.userAuthId))
        {
            bool isServerItself = request.ClientNetworkId == NetworkManager.ServerClientId;
            if (!isServerItself)
            {
                Debug.LogWarning($"[NetworkServer] Rejecting client {request.ClientNetworkId}: missing or invalid UserData payload.");
            }

            response.Approved = isServerItself;
            response.CreatePlayerObject = false;
            response.Reason = isServerItself ? string.Empty : "Missing or invalid user data.";
            return;
        }

        // Store the mappings so we can resolve them later.
        clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
        authIdToUserData[userData.userAuthId] = userData;

        OnUserJoined?.Invoke(userData);


        // Approve the connection and tell Netcode where/whether to spawn the player object.
        response.Approved = true;
        response.Position = SpawnPoint.GetRandomSpawnPos();
        response.Rotation = Quaternion.identity;
        response.CreatePlayerObject = true;

    }


    // Hooks up the disconnect callback once the server has actually started.
    private void OnNetworkReady()
    {
        // The server can start once per match, so make sure the handler is only subscribed once.
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;

    }

    // Called by Netcode when a client disconnects. Fires OnUserLeft so the
    // lobby can remove the user, and clears the lookup tables.
    private void OnClientDisconnect(ulong clientId)
    {
        if (clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            // Notify the host (e.g. HostGameManager) to update the lobby.
            OnClientLeft?.Invoke(authId);
            OnUserLeft?.Invoke(authIdToUserData[authId]);
            // Clean up the lookups.
            clientIdToAuth.Remove(clientId);
            authIdToUserData.Remove(authId);
        }

    }

    // Unsubscribes from all events and shuts down the NetworkManager if it is still running.
    public void Dispose()
    {
        if (networkManager == null) { return; }


        networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnServerStarted -= OnNetworkReady;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();

        }
    }
}
