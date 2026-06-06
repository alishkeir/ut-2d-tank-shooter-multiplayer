using System;
using System.Collections;
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
    private const int _MAX_CONNECTIONS = 20;
    private const string _GAME_SCENE_NAME = "Game";

    private Allocation allocation;
    private string joinCode;

    private Lobby lobby;

    private NetworkServer networkServer;


    public void Dispose()
    {
        try
        {

            if (lobby != null)
            {
                LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }

        lobby = null;
        networkServer?.Dispose();

    }


    public async Task StartHostAsync()
    {
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(_MAX_CONNECTIONS);

        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        try
        {
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Join Code: {joinCode}");

        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls"); //TODO: check for "udp"
        transport.SetRelayServerData(relayServerData);

        try
        {
            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
               {
                   { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
               }
            };

            string playerName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player");

            lobby = await LobbyService.Instance.CreateLobbyAsync($"{playerName}'s Lobby", _MAX_CONNECTIONS, lobbyOptions);



        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return;
        }

        networkServer = new NetworkServer(NetworkManager.Singleton);


        UserData userData = new()
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Player"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;


        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(_GAME_SCENE_NAME, LoadSceneMode.Single);

    }


    public async Task ShutdownAsync()
    {
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

        networkServer?.Dispose();
        networkServer = null;
    }



}