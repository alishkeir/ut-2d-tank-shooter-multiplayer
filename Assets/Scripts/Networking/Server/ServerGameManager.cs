// ServerGameManager
// -----------------
// Non-MonoBehaviour class that owns the server-side networking logic for a
// dedicated game server. Instantiated by ServerSingleton on startup and
// disposed on shutdown.
//
// Responsibilities:
// - Await the Multiplay allocation payload (players, teams, match info).
// - Create a server-hosted Session (the Unity 6 Session system replaces
//   the old MultiplayEventCallbacks / IServerQueryHandler APIs).
// - Open the Netcode transport connection.
// - Load the Game scene.
//
// Used by: ServerSingleton, ApplicationController.
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerGameManager : IDisposable
{
    private string serverIP;
    private int serverPort;
    private int queryPort;
    private NetworkServer networkServer;
    private MultiplayAllocationService multiplayAllocationService;

    private const string GameSceneName = "Game";

    public ServerGameManager(string serverIP, int serverPort, int queryPort, NetworkManager manager)
    {
        this.serverIP = serverIP;
        this.serverPort = serverPort;
        this.queryPort = queryPort;
        networkServer = new NetworkServer(manager);
        multiplayAllocationService = new MultiplayAllocationService();
    }

    public async Task StartGameServerAsync()
    {
        // 1. Wait for Multiplay to allocate this server and give us the
        //    matchmaking payload (players, teams, queue info).
        MatchmakingResults allocationPayload = await multiplayAllocationService.AwaitAllocationAsync();

        // 2. Create a server-hosted Session.  The session system will
        //    automatically manage backfill tickets, heartbeat, and player
        //    tracking using the matchmaker info from the payload.
        //
        //    If running locally (no payload), StartSessionAsync gracefully
        //    handles the null payload and still creates a valid session.
        await multiplayAllocationService.StartSessionAsync(
            allocationPayload,
            maxPlayers: 20,
            sessionName: $"Game-{serverIP}:{serverPort}"
        );

        // 3. Open the Netcode transport so clients can connect.
        if (!networkServer.OpenConnection(serverIP, serverPort))
        {
            Debug.LogWarning("NetworkServer did not start as expected.");
            return;
        }

        // 4. Load the gameplay scene for all connected players.
        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    public void Dispose()
    {
        multiplayAllocationService?.Dispose();
        networkServer?.Dispose();
    }
}
