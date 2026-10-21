
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

public enum MatchmakerPollingResult
{
    Success,
    TicketCreationError,
    TicketCancellationError,
    TicketRetrievalError,
    MatchAssignmentError
}

public class MatchmakingResult
{
    public string ip;
    public int port;
    public MatchmakerPollingResult result;
    public string resultMessage;
}

public class MatchplayMatchmaker : IDisposable
{
    private CancellationTokenSource cancelToken;

    public bool IsMatchmaking { get; private set; }

    public async Task<MatchmakingResult> Matchmake(UserData data)
    {
        cancelToken?.Dispose();
        cancelToken = new CancellationTokenSource();

        IsMatchmaking = true;

        try
        {
            var options = new MatchmakerOptions
            {
                QueueName = data.userGamePreferences.ToMultiplayQueue(),
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    ["localServerId"] = new PlayerProperty("oracle-1")
                }
            };

            await MultiplayerService.Instance.MatchmakeSessionAsync(
                options,
                new SessionOptions { MaxPlayers = 1 },
                cancelToken.Token
            );

            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.Success,
                resultMessage = "Joined match session."
            };
        }
        catch (OperationCanceledException)
        {
            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.TicketCancellationError,
                resultMessage = "Matchmaking cancelled."
            };
        }
        catch (SessionException e)
        {
            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.MatchAssignmentError,
                resultMessage = e.ToString()
            };
        }
        catch (Exception e)
        {
            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.TicketCreationError,
                resultMessage = e.ToString()
            };
        }
        finally
        {
            IsMatchmaking = false;
        }
    }

    public Task CancelMatchmaking()
    {
        cancelToken?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        cancelToken?.Cancel();
        cancelToken?.Dispose();
        cancelToken = null;
    }
}
