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

    private NetworkClient networkClient;
    private JoinAllocation allocation;
    private string joinedLobbyId;

    public void Dispose()
    {
        LeaveLobby();
        networkClient?.Dispose();
    }

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

    public async Task<bool> InitAsync()
    {
        // Authenticate player
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(NetworkManager.Singleton);

        AuthState authState = await AuthenticationHandler.DoAuth();

        if (authState == AuthState.Authenticated)
        {
            return true;
        }
        return false;
    }

    public async Task StartClientAsync(string joinCode)
    {
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");

        transport.SetRelayServerData(relayServerData);

        UserData userData = new()
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        NetworkManager.Singleton.StartClient();
    }

    public void SetJoinedLobbyId(string lobbyId)
    {
        joinedLobbyId = lobbyId;
    }
}
