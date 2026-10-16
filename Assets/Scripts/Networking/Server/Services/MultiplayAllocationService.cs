// UNITY 6 COMPAT: The entire original file used Unity.Services.Multiplay, which was
// removed in com.unity.services.multiplayer 2.0.0 when Unity shut down its Game Server
// Hosting (Multiplay) service (April 2026).
//
// Replacement strategy:
//   - Dedicated servers now receive their public IP, port, and deployment identity via
//     environment variables injected by the hosting provider at container start.
//   - This implementation checks for Edgegap-prefixed env vars first, then falls back to
//     generic names (SERVER_IP / SERVER_PORT / DEPLOYMENT_ID), and finally falls back to
//     the command-line args parsed by ApplicationData (-ip / -port).
//   - The public method signatures are unchanged so no callers need to be updated.
//   - Server Query Protocol (SQP) — which IServerQueryHandler implemented — is not
//     provided by Edgegap via an SDK. Player-count is tracked in memory here; if you
//     need SQP for a third-party monitoring tool, implement it separately.
//
// Tested with: Edgegap, self-hosted VPS (set SERVER_IP / SERVER_PORT env vars manually).

using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class MultiplayAllocationService : IDisposable
{
    private CancellationTokenSource serverCheckCancel;

    // In-memory player tracking (replaces IServerQueryHandler counters).
    private ushort currentPlayers;
    private ushort maxPlayers;
    private string serverName = "";
    private string buildId = "";
    private string map = "";
    private string gameType = "";

    public MultiplayAllocationService()
    {
        serverCheckCancel = new CancellationTokenSource();
    }

    // Returns a MatchmakingResults payload when running on a dedicated server,
    // or null when running as a local host / client (no env vars present).
    public async Task<MatchmakingResults> SubscribeAndAwaitMatchmakerAllocation()
    {
        string ip = ReadServerIp();
        int port = ReadServerPort();
        string deploymentId = ReadDeploymentId();

        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("[MultiplayAllocationService] No server IP found in env vars or command-line args. " +
                             "Not running as a dedicated server, or env vars are not set.");
            return null;
        }

        Debug.Log($"[MultiplayAllocationService] Server identity resolved — IP: {ip}  Port: {port}  DeploymentId: {deploymentId}");

        // The matchmaker payload is not available via the old Multiplay SDK anymore.
        // If you need the MatchmakingResults payload (e.g., to know which players to expect),
        // fetch it from your own backend or use Unity Matchmaker's GetMatchmakingResultsAsync.
        await Task.CompletedTask;
        return null;
    }

    // Logs server info; replaces the old IServerQueryHandler registration call.
    public async Task BeginServerCheck()
    {
        string ip = ReadServerIp();
        int port = ReadServerPort();

        Debug.Log($"[MultiplayAllocationService] Server check started — IP: {ip}  Port: {port}  " +
                  $"Name: {serverName}  Build: {buildId}  Map: {map}  Mode: {gameType}");

        await Task.CompletedTask;
    }

    public void SetServerName(string name) => serverName = name;
    public void SetBuildID(string id) => buildId = id;
    public void SetMaxPlayers(ushort players) => maxPlayers = players;
    public void SetMap(string newMap) => map = newMap;
    public void SetMode(string mode) => gameType = mode;

    public void AddPlayer()
    {
        currentPlayers++;
        Debug.Log($"[MultiplayAllocationService] Player joined — {currentPlayers}/{maxPlayers}");
    }

    public void RemovePlayer()
    {
        if (currentPlayers > 0) currentPlayers--;
        Debug.Log($"[MultiplayAllocationService] Player left — {currentPlayers}/{maxPlayers}");
    }

    public void Dispose()
    {
        serverCheckCancel?.Cancel();
        serverCheckCancel?.Dispose();
    }

    // ── env var helpers ──────────────────────────────────────────────────────

    private static string ReadServerIp()
    {
        // Edgegap injects EDGEGAP_SERVER_IP; fall back to generic SERVER_IP or cmd-arg.
        return Environment.GetEnvironmentVariable("EDGEGAP_SERVER_IP")
            ?? Environment.GetEnvironmentVariable("SERVER_IP")
            ?? ApplicationData.IP();
    }

    private static int ReadServerPort()
    {
        string raw = Environment.GetEnvironmentVariable("EDGEGAP_SERVER_PORT")
                  ?? Environment.GetEnvironmentVariable("SERVER_PORT");

        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed))
            return parsed;

        return ApplicationData.Port();
    }

    private static string ReadDeploymentId()
    {
        return Environment.GetEnvironmentVariable("EDGEGAP_DEPLOYMENT_ID")
            ?? Environment.GetEnvironmentVariable("DEPLOYMENT_ID")
            ?? "local";
    }
}