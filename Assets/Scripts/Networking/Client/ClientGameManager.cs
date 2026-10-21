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
using System.Text.RegularExpressions;

public class ClientGameManager : IDisposable
{
    private const string MenuSceneName = "MainMenu";

    private NetworkClient networkClient;
    private MatchplayMatchmaker matchmaker;


    JoinAllocation allocation;
    UserData userData;

    // The matchmade session we are in (or null). Fully qualified to avoid clashing with the other UGS namespaces here.
    Unity.Services.Multiplayer.ISession matchSession;

    private string joinedLobbyId;        // The lobby ID we are currently a member of (or null).
    private bool isStartingClient = false;   // True while we are starting the client.

    public async Task<bool> InitAsync()
    {
        await UnityServices.InitializeAsync();

        networkClient = new NetworkClient(networkManager: NetworkManager.Singleton);
        matchmaker = new MatchplayMatchmaker();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        userData = new UserData
        {
            userName = PlayerPrefs.GetString(NameSelector.PlayerNameKey, "Missing Name"),
            userAuthId = AuthenticationService.Instance.PlayerId
        };

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

        ConnectClient();
    }

    void ConnectClient()
    {
        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        NetworkManager.Singleton.StartClient();

        isStartingClient = false;
    }

    public void StartClient(string ip, int port)
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, (ushort)port);
        ConnectClient();
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
        _ = DisconnectAsync();
    }

    // When we are in a matchmade session, leave it through the SDK: it stops the NetworkManager itself and
    // removes us from the session. Shutting the NetworkManager down directly leaves the session thinking it is
    // still connected, which later logs "NetworkManagerSession.StopAsync: Called after dispose" on quit.
    async Task DisconnectAsync()
    {
        var session = matchSession;
        matchSession = null;

        if (session != null)
        {
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaving the match session failed: {e.Message}");
            }
        }

        // Loads the menu and makes sure the NetworkManager is down (also covers non-session connections).
        networkClient.Disconnect();
    }

    public async void MatchMakeAsync(Action<MatchmakerPollingResult> onMatchmakingResponse)
    {
        if (matchmaker.IsMatchmaking) return;

        MatchmakerPollingResult matchResult = await GetMatchAsync();

        onMatchmakingResponse.Invoke(matchResult);
    }

    async Task<MatchmakerPollingResult> GetMatchAsync()
    {
        string payload = JsonUtility.ToJson(userData);
        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            Encoding.UTF8.GetBytes(payload);

        MatchmakingResult matchmakingResult = await matchmaker.Matchmake(userData);

        if (matchmakingResult.result != MatchmakerPollingResult.Success)
        {
            Debug.LogError($"Matchmaking failed: {matchmakingResult.resultMessage}");
        }
        else
        {
            matchSession = matchmakingResult.session;
        }

        return matchmakingResult.result;
    }

    // Called by ClientSingleton.OnDestroy. Leaves any joined lobby and disposes the NetworkClient.
    public void Dispose()
    {
        LeaveLobby();
        networkClient?.Dispose();
    }

    public async Task CancelMatchmaking()
    {
        await matchmaker.CancelMatchmaking();
    }
}