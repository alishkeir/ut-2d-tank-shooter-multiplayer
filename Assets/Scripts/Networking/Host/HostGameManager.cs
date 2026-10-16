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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay.Models; // UNITY 6 COMPAT: AllocationUtils moved here from Unity.Networking.Transport.Relay
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostGameManager : IDisposable
{
    private Allocation allocation;
    private Coroutine heartbeatCoroutine;
    private string joinCode;
    private string lobbyId;

    private const int maxConnections = 20;
    private const string GameSceneName = "Game";

    public NetworkServer NetworkServerInstance { get; private set; }

    public async Task StartHostAsync()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections: maxConnections);
        }
        catch (Exception exc)
        {
            Debug.LogError(exc);
            return;
        }

        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocationId: allocation.AllocationId);
            Debug.Log(joinCode);
        }
        catch (Exception exc)
        {
            Debug.LogError(exc);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);

        try
        {
            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions();
            lobbyOptions.IsPrivate = false;
            lobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: joinCode)
                }
            };
            string playerName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Unknown");

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                $"{playerName}'s Lobby", maxConnections, options: lobbyOptions);

            lobbyId = lobby.Id;

            heartbeatCoroutine = HostSingleton.Instance.StartCoroutine(HeartbeatLobby(15));
        }
        catch (LobbyServiceException lobbyServiceException)
        {
            Debug.LogError(lobbyServiceException);
            return;
        }

        NetworkServerInstance = new NetworkServer(networkManager: NetworkManager.Singleton);

        UserData userData = new UserData
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Missing Name"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        NetworkManager.Singleton.StartHost();

        NetworkServerInstance.OnUserLeft += HandleClientLeft;

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    private async void HandleClientLeft(string authId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, authId);
        }
        catch (LobbyServiceException lobbyServiceException)
        {
            Debug.LogError(lobbyServiceException);
        }
    }

    private IEnumerator HeartbeatLobby(float waitTimeSeconds)
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    public async void Dispose()
    {
        await ShutdownAsync();
    }


    public async Task ShutdownAsync()
    {
        if (heartbeatCoroutine != null)
        {
            HostSingleton.Instance.StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
        if (!string.IsNullOrEmpty(lobbyId))
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            }
            catch (LobbyServiceException lobbyServiceException)
            {

                if (lobbyServiceException.Reason != LobbyExceptionReason.LobbyNotFound)
                {
                    Debug.LogError(lobbyServiceException);
                }
            }

            lobbyId = string.Empty;
        }

        if (NetworkServerInstance != null)
        {
            NetworkServerInstance.OnUserLeft -= HandleClientLeft;
            NetworkServerInstance.Dispose();
            NetworkServerInstance = null;
        }
    }

}