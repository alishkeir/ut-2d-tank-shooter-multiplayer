// ServerGameManager
// -----------------
// Non-MonoBehaviour class that owns the server-side logic for a dedicated
// game server. Instantiated by ServerSingleton, disposed on shutdown.
//
// Responsibilities:
// - Sign in to UGS with the service account (env vars).
// - Create the control session and watch it for the allocator's pendingMatchId.
// - Create a match session for each pending match (Relay or Direct); the
//   session's network handler starts the Netcode server.
// - Keep NetworkServer alive for connection approval / UserData tracking.
// - Load the Game scene whenever the Netcode server (re)starts.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_SERVER
using Unity.Services.Authentication.Server;
#endif

public class ServerGameManager : IDisposable
{
    public NetworkServer NetworkServer { get; private set; }


    readonly NetworkManager networkManager;
    readonly string localServerId;
    readonly bool useRelay;
    readonly int maxPlayers;
    readonly string gameSceneName;
    readonly bool enableBackfill;
    readonly string relayRegion;

    int playerCount;



#if UNITY_SERVER
    // Must match the allocator module's constants.
    const string AllocatorPlayerId = "LocalServerAllocator00000000";
    const string PendingMatchIdKey = "pendingMatchId";
    const string ControlSessionPrefix = "local-";

    // Local testing only; load from a real secret store in production.
    const string ServiceAccountKeyIdVariable = "LOCAL_SERVER_SERVICE_ACCOUNT_KEY_ID";
    const string ServiceAccountSecretVariable = "LOCAL_SERVER_SERVICE_ACCOUNT_SECRET";

    IServerSession controlSession;
    IServerSession matchSession;
    string lastHandledMatchId;
    bool disposed;

    // Creating and deleting match sessions must not overlap (a new match vs. closing an empty one).
    readonly SemaphoreSlim sessionLock = new SemaphoreSlim(1, 1);
#endif

    public ServerGameManager(NetworkManager manager, string localServerId, bool useRelay, int maxPlayers, string gameSceneName, NetworkObject playerPrefab,
        bool enableBackfill = false, string relayRegion = null)
    {
        networkManager = manager;
        this.localServerId = localServerId;
        this.useRelay = useRelay;
        this.maxPlayers = maxPlayers;
        this.gameSceneName = gameSceneName;
        this.enableBackfill = enableBackfill;
        this.relayRegion = relayRegion;

        // Must exist BEFORE the match session starts the server, so approval is hooked in time.
        NetworkServer = new NetworkServer(manager, playerPrefab);
        NetworkServer.OnUserJoined += UserJoined;
        NetworkServer.OnUserLeft += UserLeft;
    }

    public async Task StartGameServerAsync()
    {
#if UNITY_SERVER
        // Load the Game scene every time the session spins the Netcode server up.
        networkManager.OnServerStarted += OnServerStarted;

        try
        {
            var keyId = Environment.GetEnvironmentVariable(ServiceAccountKeyIdVariable)?.Trim().Trim('"', '\'');
            var secret = Environment.GetEnvironmentVariable(ServiceAccountSecretVariable)?.Trim().Trim('"', '\'');

            Debug.Log($"Key ID length: {keyId?.Length ?? 0}, Secret length: {secret?.Length ?? 0}");

            await ServerAuthenticationService.Instance.SignInWithServiceAccountAsync(keyId, secret);

            // The control session only carries the match id, so it needs no network.
            controlSession = await MultiplayerServerService.Instance.CreateSessionAsync(
                ControlSessionPrefix + localServerId,
                new SessionOptions
                {
                    Name = ControlSessionPrefix + localServerId,
                    IsPrivate = true,
                    MaxPlayers = 2
                });

            // The allocator joins once, then updates its player data for every later match.
            controlSession.PlayerJoined += _ => OnControlSessionChanged();
            controlSession.PlayerPropertiesChanged += OnControlSessionChanged;
            Debug.Log($"Control session '{controlSession.Id}' ready.");

            _ = WatchControlSessionAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
#else
        Debug.LogWarning("ServerGameManager.StartGameServerAsync only runs in UNITY_SERVER builds.");
        await Task.CompletedTask;
#endif
    }

#if UNITY_SERVER
    void OnControlSessionChanged()
    {
        Debug.Log($"[{Stamp()}] Control session event received.");
        _ = CreatePendingMatchSessionAsync("event");
    }

    // Safety net: the allocator hands over later matches by updating its player data, which only raises
    // PlayerPropertiesChanged. If that event is ever missed, re-read the control session so the match isn't lost.
    async Task WatchControlSessionAsync()
    {
        while (!disposed && controlSession != null)
        {
            await Task.Delay(2000);
            if (disposed) { return; }

            await CreatePendingMatchSessionAsync("poll");
        }
    }

    async Task CreatePendingMatchSessionAsync(string source)
    {
        var allocator = controlSession.Players.FirstOrDefault(player => player.Id == AllocatorPlayerId);
        if (allocator == null ||
            !allocator.Properties.TryGetValue(PendingMatchIdKey, out var pendingMatchId) ||
            string.IsNullOrEmpty(pendingMatchId.Value) ||
            pendingMatchId.Value == lastHandledMatchId)
        {
            return;
        }

        var matchId = pendingMatchId.Value;
        lastHandledMatchId = matchId;

        var timer = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log($"[{Stamp()}] Pending match '{matchId}' received from the allocator (via {source}). Creating match session...");

        await sessionLock.WaitAsync();
        try
        {
            // One NetworkManager runs one match at a time, so the next match replaces the current one.
            if (matchSession != null)
            {
                var previous = matchSession;
                matchSession = null;
                await previous.DeleteAsync();
            }

            // Players of the replaced match disconnect while it shuts down; start counting from zero.
            playerCount = 0;

            // Don't use the same SessionOptions.Type as the control session: the SDK keeps one session per type.
            var options = new SessionOptions { MaxPlayers = maxPlayers };

            // An explicit Relay region skips the QoS latency measurements, which otherwise run on every match creation.
            var region = string.IsNullOrWhiteSpace(relayRegion) ? null : relayRegion.Trim();
            options = useRelay ? options.WithRelayNetwork(region) : options.WithDirectNetwork();

            // The SDK starts backfilling automatically when a player leaves a matchmade session. That needs the
            // "Matchmaker Backfill Admin" role on the service account, otherwise the call fails with a 403.
            // Nothing is backfilled unless enableBackfill is true.
            options = options.WithBackfillingConfiguration(
                enable: enableBackfill,
                automaticallyRemovePlayers: enableBackfill,
                autoStart: enableBackfill,
                playerConnectionTimeout: 30,
                backfillingLoopInterval: 1);

            matchSession = await MultiplayerServerService.Instance.CreateMatchSessionAsync(matchId, options);
            matchSession.PlayerJoined += playerId => Debug.Log($"[{Stamp()}] Player joined match {matchId}: {playerId}");
            Debug.Log($"[{Stamp()}] Match session '{matchSession.Id}' created ({(useRelay ? "Relay" : "Direct")}) " +
                      $"in {timer.Elapsed.TotalSeconds:0.00}s. Backfill {(enableBackfill ? "enabled" : "disabled")}.");
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
        finally
        {
            sessionLock.Release();
        }
    }

    // Deletes the match session once its last player is gone, so a dead match (for example after a client
    // process was killed) doesn't keep the lobby, Relay allocation and matchmaker state around.
    async Task CloseEmptyMatchSessionAsync(IServerSession session)
    {
        if (session == null) { return; }

        // Leave the Netcode disconnect callback before shutting the NetworkManager down.
        await Task.Yield();

        await sessionLock.WaitAsync();
        try
        {
            // A newer match may have replaced it, or a player may have joined in the meantime.
            if (!ReferenceEquals(matchSession, session) || playerCount > 0) { return; }

            matchSession = null;
            Debug.Log($"[{Stamp()}] Match session '{session.Id}' is empty. Closing it so the server is free for the next match.");
            await session.DeleteAsync();
            Debug.Log($"[{Stamp()}] Match session '{session.Id}' closed.");
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
        finally
        {
            sessionLock.Release();
        }
    }

    void OnServerStarted()
    {
        networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    // The Player log has no timestamps, so add one to the lines used to measure matchmaking latency.
    static string Stamp() => DateTime.UtcNow.ToString("HH:mm:ss.fff");

    // The server session APIs surface failures wrapped in AggregateException.
    static Exception Unwrap(Exception exception) =>
        exception is AggregateException aggregate ? aggregate.Flatten().InnerException ?? exception : exception;
#endif

    void UserJoined(UserData userData)
    {
        playerCount++;
        Debug.Log($"[Server] {userData.userName} joined — {playerCount}/{maxPlayers}");
    }

    void UserLeft(UserData userData)
    {
        if (playerCount > 0) playerCount--;
        Debug.Log($"[Server] {userData.userName} left — {playerCount}/{maxPlayers}");

#if UNITY_SERVER
        // matchSession is null while a match is being replaced, so players of the old match can't close the new one.
        if (playerCount == 0) { _ = CloseEmptyMatchSessionAsync(matchSession); }
#endif
    }

    public void Dispose()
    {
        if (NetworkServer != null)
        {
            NetworkServer.OnUserJoined -= UserJoined;
            NetworkServer.OnUserLeft -= UserLeft;
        }

#if UNITY_SERVER
        disposed = true;
        if (networkManager != null) networkManager.OnServerStarted -= OnServerStarted;
        _ = matchSession?.DeleteAsync();
        _ = controlSession?.DeleteAsync();
#endif

        NetworkServer?.Dispose();
    }
}