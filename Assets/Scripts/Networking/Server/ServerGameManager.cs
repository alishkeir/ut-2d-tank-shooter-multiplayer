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
#endif

    public ServerGameManager(NetworkManager manager, string localServerId, bool useRelay, int maxPlayers, string gameSceneName)
    {
        networkManager = manager;
        this.localServerId = localServerId;
        this.useRelay = useRelay;
        this.maxPlayers = maxPlayers;
        this.gameSceneName = gameSceneName;

        // Must exist BEFORE the match session starts the server, so approval is hooked in time.
        NetworkServer = new NetworkServer(manager);
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
    void OnControlSessionChanged() => _ = CreatePendingMatchSessionAsync();

    async Task CreatePendingMatchSessionAsync()
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

        try
        {
            // One NetworkManager runs one match at a time, so the next match replaces the current one.
            if (matchSession != null)
            {
                var previous = matchSession;
                matchSession = null;
                await previous.DeleteAsync();
            }

            // Don't use the same SessionOptions.Type as the control session: the SDK keeps one session per type.
            var options = new SessionOptions { MaxPlayers = maxPlayers };
            options = useRelay ? options.WithRelayNetwork() : options.WithDirectNetwork();

            matchSession = await MultiplayerServerService.Instance.CreateMatchSessionAsync(matchId, options);
            matchSession.PlayerJoined += playerId => Debug.Log($"Player joined match {matchId}: {playerId}");
            Debug.Log($"Match session '{matchSession.Id}' created ({(useRelay ? "Relay" : "Direct")}).");
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
    }

    void OnServerStarted()
    {
        networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

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
        // Hook point: close the match / quit when empty, if you want that behaviour.
    }

    public void Dispose()
    {
        if (NetworkServer != null)
        {
            NetworkServer.OnUserJoined -= UserJoined;
            NetworkServer.OnUserLeft -= UserLeft;
        }

#if UNITY_SERVER
        if (networkManager != null) networkManager.OnServerStarted -= OnServerStarted;
        _ = matchSession?.DeleteAsync();
        _ = controlSession?.DeleteAsync();
#endif

        NetworkServer?.Dispose();
    }
}