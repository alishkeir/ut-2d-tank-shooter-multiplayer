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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerGameManager : IDisposable
{
    string serverIP;
    int serverPort;
    int queryPort;

    NetworkServer networkServer;
    MultiplayAllocationService multiplayAllocationService;
    MatchplayBackfiller backfiller;

    const string GameSceneName = "Game";

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

        await multiplayAllocationService.BeginServerCheck();

        try
        {
            MatchmakingResults matchmakerPayload = await GetMatchMakerPayload();

            if (matchmakerPayload != null)
            {
                await StartBackfill(matchmakerPayload);
                networkServer.OnUserJoined += UserJoined;
                networkServer.OnUserLeft += UserLeft;

            }
            else
            {
                Debug.LogWarning("No Matchmaking Results");
            }
        }
        catch (Exception e)
        {

            Debug.LogWarning(e);
        }

        if (!networkServer.OpenConnection(serverIP, serverPort))
        {
            Debug.LogWarning("NetworkServer did not start as expected.");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    async Task StartBackfill(MatchmakingResults payload)
    {
        backfiller = new MatchplayBackfiller($"{serverIP}:{serverPort}", payload.QueueName, payload.MatchProperties, 20);

        if (backfiller.NeedsPlayers())
            await backfiller.BeginBackfilling();
    }

    async Task<MatchmakingResults> GetMatchMakerPayload()
    {
        Task<MatchmakingResults> matchmakerPayloadTask = multiplayAllocationService.SubscribeAndAwaitMatchmakerAllocation();

        if (await Task.WhenAny(matchmakerPayloadTask, Task.Delay(20000)) == matchmakerPayloadTask)
        {
            return matchmakerPayloadTask.Result;
        }

        return null;
    }

    void UserJoined(UserData userData)
    {
        backfiller.AddPlayerToMatch(userData);
        multiplayAllocationService.AddPlayer();
        if (!backfiller.NeedsPlayers() && backfiller.IsBackfilling)
        {
            _ = backfiller.StopBackfill();
        }
    }

    void UserLeft(UserData userData)
    {
        int playerCount = backfiller.RemovePlayerFromMatch(userData.userAuthId);
        multiplayAllocationService.RemovePlayer();

        if (playerCount <= 0)
        {
            CloseServer();
            return;
        }

        if (backfiller.NeedsPlayers() && !backfiller.IsBackfilling)
        {
            _ = backfiller.BeginBackfilling();
        }
    }

    async void CloseServer()
    {
        await backfiller.StopBackfill();
        Dispose();
        Application.Quit();
    }


    public void Dispose()
    {
        networkServer.OnUserJoined -= UserJoined;
        networkServer.OnUserLeft -= UserLeft;

        backfiller?.Dispose();
        multiplayAllocationService?.Dispose();
        networkServer?.Dispose();
    }
}
