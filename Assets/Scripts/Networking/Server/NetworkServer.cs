
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkServer : IDisposable
{

    private NetworkManager networkManager;

    public event Action<string> OnUserLeft;

    private Dictionary<ulong, string> clientIdToAuth = new();
    private Dictionary<string, UserData> authIdToUserData = new();


    public NetworkServer(NetworkManager networkManager)
    {
        this.networkManager = networkManager;

        networkManager.ConnectionApprovalCallback += ApprovalCheck;
        networkManager.OnServerStarted += OnNetworkReady;

    }

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


    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
        UserData userData = JsonUtility.FromJson<UserData>(payload);

        clientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
        authIdToUserData[userData.userAuthId] = userData;


        response.Approved = true;
        response.Position = SpawnPoint.GetRandomSpawnPos();
        response.Rotation = Quaternion.identity;
        response.CreatePlayerObject = true;

    }


    private void OnNetworkReady()
    {
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;

    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientIdToAuth.TryGetValue(clientId, out string authId))
        {
            OnUserLeft?.Invoke(authId);
            clientIdToAuth.Remove(clientId);
            authIdToUserData.Remove(authId);
        }

    }

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
