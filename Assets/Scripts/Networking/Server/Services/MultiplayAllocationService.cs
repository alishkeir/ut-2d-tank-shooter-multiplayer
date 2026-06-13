using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Manages Multiplay server lifecycle using the Unity 6 multiplayer services SDK.
///
/// In Unity 6 the old Unity.Services.Multiplay APIs (IMultiplayService, MultiplayEventCallbacks,
/// IServerQueryHandler) have been removed.  Instead, the server creates an IHostSession via
/// MultiplayerService.CreateSessionAsync, and the session system automatically:
///   - Reads the Multiplay allocation payload from environment variables / config
///   - Manages backfill tickets via the matchmaker module
///   - Tracks player join/leave events
///   - Handles the server heartbeat
///
/// Usage (server bootstrap):
///   var allocSvc = new MultiplayAllocationService();
///   MatchmakingResults payload = await allocSvc.AwaitAllocationAsync();
///   await allocSvc.StartSessionAsync(payload, maxPlayers: 10);
///   // Use allocSvc.ServerSession to manage the game
/// </summary>
public class MultiplayAllocationService : IDisposable
{
    private CancellationTokenSource _serverCheckCancel;

    // The dedicated-server session handle (non-null once StartSessionAsync completes).
    public IHostSession ServerSession { get; private set; }

    // Tracks current player count from session events.
    private int _currentPlayerCount;

    /// <summary>
    /// The current number of players in the session.
    /// </summary>
    public int CurrentPlayerCount => _currentPlayerCount;

    public MultiplayAllocationService()
    {
        _serverCheckCancel = new CancellationTokenSource();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Allocation – read the Multiplay payload
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits for the Multiplay allocation payload to become available (either
    /// from environment variables or by polling the server config) and returns
    /// the deserialised <see cref="MatchmakingResults"/>.
    /// </summary>
    public async Task<MatchmakingResults> AwaitAllocationAsync()
    {
        // First attempt: environment variables that Multiplay injects into
        // the game-server process.
        string payload = Environment.GetEnvironmentVariable("UNITY_MATCHER_PAYLOAD");

        if (!string.IsNullOrEmpty(payload))
        {
            try
            {
                var results = JsonConvert.DeserializeObject<MatchmakingResults>(payload);
                Debug.Log($"[Alloc] Payload from env. Match: {results?.MatchId}");
                return results;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Alloc] Failed to deserialize UNITY_MATCHER_PAYLOAD: {ex.Message}");
            }
        }

        // Fallback: poll for the ServerConfig file (written by Multiplay).
        // This is the path used in production game-server deployments.
        payload = null;
        Debug.Log("[Alloc] Polling for allocation config…");

        while (string.IsNullOrEmpty(payload))
        {
            try
            {
                var config = ReadAllocationConfig();
                if (config.HasValue)
                {
                    payload = config.Value.payload;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        var results = JsonConvert.DeserializeObject<MatchmakingResults>(payload);
                        Debug.Log($"[Alloc] Config resolved. AllocID: {config.Value.allocationId}, Match: {results?.MatchId}");
                        return results;
                    }
                }
            }
            catch
            {
                // Not available yet – keep polling.
            }

            await Task.Delay(500);
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Session creation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a dedicated-server-hosted session that represents this game
    /// instance.  The session system handles backfill tickets, player tracking,
    /// and Multiplay heartbeat automatically.
    /// </summary>
    /// <param name="payload">The matchmaking allocation payload.</param>
    /// <param name="maxPlayers">Maximum players for the session.</param>
    /// <param name="sessionName">Optional display name for the session.</param>
    public async Task<IHostSession> StartSessionAsync(
        MatchmakingResults payload = null,
        int maxPlayers = 20,
        string sessionName = null)
    {
        var service = MultiplayerService.Instance;
        if (service == null)
        {
            Debug.LogError("[Alloc] MultiplayerService.Instance is null – has UnityServices been initialized?");
            return null;
        }

        string matchId = payload?.MatchId;

        var options = new SessionOptions
        {
            Name = sessionName ?? $"Game-{matchId ?? Guid.NewGuid().ToString()}",
            MaxPlayers = maxPlayers,
            IsPrivate = false,
            IsLocked = false,
            Type = "Server",
        };

        // Store matchmaker info in session properties so the matchmaker
        // module can associate this session with the matchmaker allocation.
        if (payload != null)
        {
            options.SessionProperties["matchId"] = new SessionProperty(matchId ?? "");
            options.SessionProperties["queueName"] = new SessionProperty(payload.QueueName ?? "");
            options.SessionProperties["poolId"] = new SessionProperty(payload.PoolId ?? "");
            options.SessionProperties["backfillTicketId"] = new SessionProperty(payload.BackfillTicketId ?? "");
        }

        Debug.Log($"[Alloc] Creating server session '{options.Name}' max={maxPlayers} ...");

        IHostSession hostSession;
        try
        {
            hostSession = await service.CreateSessionAsync(options);
        }
        catch (SessionException ex)
        {
            Debug.LogError($"[Alloc] Failed to create server session: {ex.Message}");
            return null;
        }

        ServerSession = hostSession;

        // Wire player tracking.
        hostSession.PlayerJoined += OnPlayerJoined;
        hostSession.PlayerHasLeft += OnPlayerLeft;

        Debug.Log($"[Alloc] Session created: {hostSession.Id}");

        // Push player count to session properties periodically.
        if (_serverCheckCancel != null && !_serverCheckCancel.IsCancellationRequested)
        {
            _ = PushPlayerCountLoopAsync(_serverCheckCancel.Token);
        }

        return hostSession;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Convenience wrappers (matching the old API surface)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the MaxPlayers value on the session.
    /// Note: in the Session SDK, MaxPlayers is read-only after creation.
    /// This stores the value in session properties for reference purposes.
    /// </summary>
    public void SetMaxPlayers(ushort players)
    {
        if (ServerSession == null) return;
        ServerSession.SetProperty("maxPlayers", new SessionProperty(players.ToString()));
        _ = ServerSession.SavePropertiesAsync();
    }

    public void SetMap(string newMap)
    {
        if (ServerSession == null) return;
        ServerSession.SetProperty("map", new SessionProperty(newMap));
        _ = ServerSession.SavePropertiesAsync();
    }

    public void SetMode(string mode)
    {
        if (ServerSession == null) return;
        ServerSession.SetProperty("gameMode", new SessionProperty(mode));
        _ = ServerSession.SavePropertiesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads allocation info from Multiplay-injected environment variables.
    /// </summary>
    private (string allocationId, string payload)? ReadAllocationConfig()
    {
        string id = Environment.GetEnvironmentVariable("MULTIPLAY_ALLOCATION_ID");
        string json = Environment.GetEnvironmentVariable("UNITY_MATCHER_PAYLOAD");

        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(json))
            return (id, json);

        return null;
    }

    private void OnPlayerJoined(string playerId)
    {
        Interlocked.Increment(ref _currentPlayerCount);
    }

    private void OnPlayerLeft(string playerId)
    {
        Interlocked.Decrement(ref _currentPlayerCount);
    }

    /// <summary>
    /// Optional loop that periodically writes the current player count into
    /// session properties so it is visible to queries.  The session system
    /// already handles the Multiplay heartbeat natively.
    /// </summary>
    private async Task PushPlayerCountLoopAsync(CancellationToken ct)
    {
        int lastCount = -1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int cur = _currentPlayerCount;
                if (cur != lastCount && ServerSession != null)
                {
                    ServerSession.SetProperty("currentPlayers", new SessionProperty(cur.ToString()));
                    await ServerSession.SavePropertiesAsync();
                    lastCount = cur;
                }
            }
            catch
            {
                // Swallow – session might be shutting down.
            }

            await Task.Delay(2000, ct);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Cleanup
    // ─────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_serverCheckCancel != null)
        {
            _serverCheckCancel.Cancel();
            _serverCheckCancel.Dispose();
            _serverCheckCancel = null;
        }

        if (ServerSession != null)
        {
            ServerSession.PlayerJoined -= OnPlayerJoined;
            ServerSession.PlayerHasLeft -= OnPlayerLeft;
            _ = ServerSession.DeleteAsync();
            ServerSession = null;
        }
    }
}
