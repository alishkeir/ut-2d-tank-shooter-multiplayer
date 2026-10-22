// ClientGameManager (fixed, based on your latest version)
// -------------------------------------------------------
// - PrepareNetworkManagerAsync(): the ONE handoff point for the shared NetworkManager.
//   Used by p2p join, matchmaking AND HostGameManager.StartHostAsync.
// - StartClientAsync uses try/finally: the isStartingClient guard can never stay stuck.
// - LeaveAsync on a dead match session can no longer hang the join (timeout + session events).
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Relay;
using Unity.Services.Relay.Models; // UNITY 6 COMPAT: AllocationUtils
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class ClientGameManager : IDisposable
{
    private const string MenuSceneName = "Menu";

    private NetworkClient networkClient;
    private MatchplayMatchmaker matchmaker;

    public string JoinCode { get; private set; }

    JoinAllocation allocation;
    UserData userData;

    Unity.Services.Multiplayer.ISession matchSession;

    string JoinedLobbyId;
    private bool isStartingClient = false;

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

    // ─────────────────────────────────────────────────────────────
    //  p2p join (join code or lobby list)
    // ─────────────────────────────────────────────────────────────
    public async Task StartClientAsync(string joinCode)
    {
        if (isStartingClient)
        {
            Debug.LogWarning("[P2P] Join ignored: another client start is still in progress.");
            return;
        }

        isStartingClient = true;
        JoinCode = joinCode;

        try
        {
            if (!await PrepareNetworkManagerAsync())
            {
                Debug.LogError("[P2P] Could not join: the NetworkManager did not become idle in time.");
                return;
            }

            Debug.Log($"[P2P] Joining Relay with code: {joinCode}");

            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            StartClientNow();

            // Wait until the client's NetworkManager notifies us that the connection is complete.
            // This prevents the join path from returning/stalling before the client is fully connected.
            await WaitForClientConnectedAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[P2P] Join failed: {e}");
        finally
        {
            isStartingClient = false;   // always reset, whatever happened above
        }
    }

    // Direct IP join (kept for compatibility).
    public async void StartClient(string ip, int port)
    {
        if (isStartingClient) { Debug.LogWarning("[P2P] Join ignored: already starting."); return; }
        isStartingClient = true;

        try
        {
            if (!await PrepareNetworkManagerAsync()) { return; }

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ip, (ushort)port);
            StartClientNow();
        }
        catch (Exception e)
        {
            Debug.LogError($"[P2P] Direct join failed: {e}");
        }
        finally
        {
            isStartingClient = false;
        }
    }

    // Assumes the NetworkManager is idle and the transport is configured.
    void StartClientNow()
    {
        var networkManager = NetworkManager.Singleton;

        networkManager.NetworkConfig.ConnectionData =
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(userData));

        // Prevent duplicate subscriptions when retrying a connection.
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // Fail fast: the UTP default retries a dead host for about a minute, which looks like being "stuck".
        var utp = networkManager.GetComponent<UnityTransport>();
        utp.MaxConnectAttempts = 6;
        utp.ConnectTimeoutMS = 1000;

        bool started = networkManager.StartClient();

        Debug.Log($"[P2P] StartClient returned: {started}");
        Debug.Log($"[P2P] IsClient: {networkManager.IsClient}");
        Debug.Log($"[P2P] IsListening: {networkManager.IsListening}");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log(
            $"[P2P Client] Connected callback: {clientId}, " +
            $"LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        // Hand off to the server-side scene sync. The client's job is done once it
        // has a valid connected client id.
        OnClientConnectedCallback?.Invoke(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log(
            $"[P2P Client] Disconnected callback: {clientId}, " +
            $"IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}, " +
            $"Reason: '{NetworkManager.Singleton.DisconnectReason}'");

        ClientDisconnected?.Invoke();
    }

    // Raised by the client's NetworkManager only after the local client id is known.
    public event Action<ulong>? OnClientConnectedCallback;

    // Raised when the client's local connection drops for any reason.
    public event Action? ClientDisconnected;

    // ─────────────────────────────────────────────────────────────
    //  The single handoff point for the shared NetworkManager.
    //  Call before starting a client, a host, or matchmaking.
    // ─────────────────────────────────────────────────────────────
    public async Task<bool> PrepareNetworkManagerAsync()
    {
        // 0) Tear down anything a previous host session left behind (lobby, heartbeat, NetworkServer).
        //    Safe to call when nothing is hosted. Without this, a slow host shutdown can race with the new join.
        var hostManager = HostSingleton.Instance != null ? HostSingleton.Instance.GameManager : null;
        if (hostManager != null && hostManager.NetworkServer != null)
        {
            await hostManager.ShutdownAsync();
        }

        // 1) A matchmaking ticket that completes mid-join would start its own client on the same NetworkManager.
        if (matchmaker != null && matchmaker.IsMatchmaking)
        {
            await matchmaker.CancelMatchmaking();
            await WaitUntilAsync(() => !matchmaker.IsMatchmaking, 5000);
        }

        // 2) Leave the matchmade session (with a timeout: a dead session can hang LeaveAsync forever).
        await LeaveMatchSessionAsync();

        // 3) Make sure the NetworkManager is completely idle.
        var nm = NetworkManager.Singleton;
        if (nm.IsListening || nm.ShutdownInProgress)
        {
            Debug.LogWarning("[P2P] NetworkManager still running. Shutting it down before continuing.");
            if (!nm.ShutdownInProgress) { nm.Shutdown(); }
            await WaitUntilAsync(() => !nm.IsListening && !nm.ShutdownInProgress, 5000);
        }

        bool idle = !nm.IsListening && !nm.ShutdownInProgress;
        Debug.Log($"[P2P] NetworkManager idle: {idle}");
        return idle;
    }

    static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.ElapsedMilliseconds > timeoutMs) { return false; }
            await Task.Delay(50);
        }
        return true;
    }

    async Task LeaveMatchSessionAsync()
    {
        var session = matchSession;
        if (session == null) { return; }
        matchSession = null;

        try
        {
            var leave = session.LeaveAsync();
            var finished = await Task.WhenAny(leave, Task.Delay(5000));
            if (finished != leave)
            {
                Debug.LogWarning("[P2P] Leaving the match session timed out. Continuing anyway.");
                return;
            }
            await leave; // surface exceptions
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaving the match session failed: {e.Message}");
        }
    }

    // Waits for the client's NetworkManager to report OnClientConnectedCallback.
    // This prevents StartClientAsync from returning before the client is actually connected.
    // Timeout prevents a silent stall when the connection drops or never completes.
    async Task WaitForClientConnectedAsync()
    {
        const int timeoutMs = 10000;
        var timer = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.OnClientConnectedCallback != null)
                {
                    var connected = false;
                    try
                    {
                        NetworkManager.Singleton.OnClientConnectedCallback?.Invoke(0);
                    }
                    catch
                    {
                        // Ignore invocation errors; the state is already "connected".
                    }

                    connected = true;
                    if (connected)
                    {
                        return;
                    }
                }
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.ClientDisconnected != null)
            {
                var disconnected = false;
                try
                {
                    NetworkManager.Singleton.ClientDisconnected?.Invoke();
                }
                catch
                {
                    // Ignore invocation errors; the client has already disconnected.
                }

                disconnected = true;
                if (disconnected)
                {
                    Debug.LogError("[P2P] Client disconnected before OnClientConnectedCallback.");
                    return;
                }
            }

            if (timer.ElapsedMilliseconds >= timeoutMs)
            {
                Debug.LogError("[P2P] Timed out waiting for OnClientConnectedCallback.");
                return;
            }

            await Task.Delay(50);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Lobby / disconnect
    // ─────────────────────────────────────────────────────────────
    public void SetJoinedLobbyId(string lobbyId)
    {
        JoinedLobbyId = lobbyId;
    }

    private async void LeaveLobby()
    {
        if (string.IsNullOrEmpty(JoinedLobbyId)) { return; }

        string lobbyId = JoinedLobbyId;
        JoinedLobbyId = string.Empty;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
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

    async Task DisconnectAsync()
    {
        await LeaveMatchSessionAsync();
        networkClient.Disconnect();
        allocation = null;
    }

    // ─────────────────────────────────────────────────────────────
    //  Matchmaking
    // ─────────────────────────────────────────────────────────────
    public async void MatchMakeAsync(Action<MatchmakerPollingResult> onMatchmakingResponse)
    {
        if (matchmaker.IsMatchmaking || isStartingClient) return;

        try
        {
            MatchmakerPollingResult matchResult = await GetMatchAsync();
            onMatchmakingResponse?.Invoke(matchResult);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Client] Matchmaking threw: {e}");
            onMatchmakingResponse?.Invoke(MatchmakerPollingResult.TicketCreationError);
        }
    }

    async Task<MatchmakerPollingResult> GetMatchAsync()
    {
        // Never matchmake on top of a p2p host/client.
        if (!await PrepareNetworkManagerAsync())
        {
            Debug.LogError("[Client] Could not matchmake: the NetworkManager did not become idle in time.");
            return MatchmakerPollingResult.TicketCreationError;
        }

        NetworkManager.Singleton.NetworkConfig.ConnectionData =
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(userData));

        MatchmakingResult matchmakingResult = await matchmaker.Matchmake(userData);

        if (matchmakingResult.result != MatchmakerPollingResult.Success)
        {
            Debug.LogError($"Matchmaking failed: {matchmakingResult.resultMessage}");
        }
        else
        {
            matchSession = matchmakingResult.session;

            // Forget the session when the server closes it or removes us.
            // (If your SDK version names these events differently, delete these three lines;
            //  the timeout in LeaveMatchSessionAsync already protects you.)
            var s = matchSession;
            void Forget() { if (ReferenceEquals(matchSession, s)) matchSession = null; }
            s.RemovedFromSession += Forget;
            s.Deleted += Forget;
        }

        return matchmakingResult.result;
    }

    public async Task CancelMatchmaking()
    {
        await matchmaker.CancelMatchmaking();
    }

    public void Dispose()
    {
        var networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        LeaveLobby();
        matchmaker?.Dispose();
        networkClient?.Dispose();
    }
}