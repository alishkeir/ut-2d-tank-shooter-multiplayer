using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
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
    private string lastUsedTicket;
    private CancellationTokenSource cancelToken;

    private const int TicketCooldown = 1000;

    public bool IsMatchmaking { get; private set; }

    public async Task<MatchmakingResult> Matchmake(UserData data)
    {
        cancelToken = new CancellationTokenSource();

        string queueName = data.userGamePreferences.ToMultiplayQueue();
        CreateTicketOptions createTicketOptions = new CreateTicketOptions(queueName);
        Debug.Log(createTicketOptions.QueueName);

        List<Player> players = new List<Player>
        {
            new Player(data.userAuthId, data.userGamePreferences)
        };

        try
        {
            IsMatchmaking = true;
            CreateTicketResponse createResult = await MatchmakerService.Instance.CreateTicketAsync(players, createTicketOptions);

            lastUsedTicket = createResult.Id;

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    TicketStatusResponse checkTicket = await MatchmakerService.Instance.GetTicketAsync(lastUsedTicket);

                    // UNITY 6 COMPAT: MultiplayAssignment was the Unity-Multiplay-specific type.
                    // Unity Multiplay hosting was shut down April 2026.  Any dedicated server
                    // that exposes an IP and port (Edgegap, self-hosted VPS, AWS, etc.) now
                    // returns IpPortAssignment.  Your UGS Matchmaker queue must be configured
                    // for "IP/Port" assignment type, not "Multiplay", for this to trigger.
                    if (checkTicket.Type == typeof(IpPortAssignment))
                    {
                        IpPortAssignment matchAssignment = (IpPortAssignment)checkTicket.Value;
                        return ReturnMatchResult(MatchmakerPollingResult.Success, "", matchAssignment);
                    }
                    // Still-pending: NoneAssignment means the ticket has not been matched yet.
                    if (checkTicket.Type == typeof(NoneAssignment))
                    {
                        Debug.Log($"Polled Ticket: {lastUsedTicket} — waiting for match...");
                    }

                    await Task.Delay(TicketCooldown);
                }
            }
            catch (MatchmakerServiceException e)
            {
                return ReturnMatchResult(MatchmakerPollingResult.TicketRetrievalError, e.ToString(), null);
            }
        }
        catch (MatchmakerServiceException e)
        {
            return ReturnMatchResult(MatchmakerPollingResult.TicketCreationError, e.ToString(), null);
        }

        return ReturnMatchResult(MatchmakerPollingResult.TicketRetrievalError, "Cancelled Matchmaking", null);
    }

    public async Task CancelMatchmaking()
    {
        if (!IsMatchmaking) { return; }

        IsMatchmaking = false;

        if (cancelToken.Token.CanBeCanceled)
        {
            cancelToken.Cancel();
        }

        if (string.IsNullOrEmpty(lastUsedTicket)) { return; }

        Debug.Log($"Cancelling {lastUsedTicket}");

        await MatchmakerService.Instance.DeleteTicketAsync(lastUsedTicket);
    }

    // UNITY 6 COMPAT: Parameter type changed from MultiplayAssignment to IpPortAssignment.
    // IpPortAssignment is the generic assignment type used by any IP/port-based server
    // (Edgegap, self-hosted VPS, AWS GameLift Anywhere, etc.).
    private MatchmakingResult ReturnMatchResult(MatchmakerPollingResult resultErrorType, string message, IpPortAssignment assignment)
    {
        IsMatchmaking = false;

        if (assignment != null)
        {
            return new MatchmakingResult
            {
                result = MatchmakerPollingResult.Success,
                ip = assignment.Ip,
                // UNITY 6 COMPAT: IpPortAssignment.Port is int? (nullable); MatchmakingResult.port
                // is a plain int, so coalesce a null port to 0 to satisfy the compiler.
                port = assignment.Port ?? 0
            };
        }

        return new MatchmakingResult
        {
            result = resultErrorType,
            resultMessage = message
        };
    }

    public void Dispose()
    {
        _ = CancelMatchmaking();

        cancelToken?.Dispose();
    }
}