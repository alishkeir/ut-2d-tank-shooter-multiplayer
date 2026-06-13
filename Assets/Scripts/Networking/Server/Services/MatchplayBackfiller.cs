using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

// Import alias to avoid ambiguity with Unity.Services.Matchmaker.Models.Player
using MatchmakerPlayer = Unity.Services.Matchmaker.Models.Player;

/// <summary>
/// Manual backfill ticket controller for Unity 6 multiplayer services SDK.
///
/// IMPORTANT: When using the Session system (MultiplayerService.CreateSessionAsync),
/// backfilling is managed automatically by the matchmaker module. You do NOT need
/// this class if you are already using sessions created through matchmaking.
///
/// Use this class only when you need explicit manual control of the backfill
/// lifecycle outside of the session system (e.g. if you manage players yourself
/// and want to add/remove players from the backfill ticket independently).
///
/// Usage:
///   var backfiller = new MatchplayBackfiller("0.0.0.0:7777", "my-queue", matchProperties);
///   await backfiller.BeginBackfilling();
///   backfiller.AddPlayerToMatch(new UserData{ userAuthId = "abc", userName = "Bob" });
///   // ... later
///   await backfiller.StopBackfill();
/// </summary>
public class MatchplayBackfiller : IDisposable
{
    private BackfillTicket _localBackfillTicket;
    private bool _localDataDirty;
    private int _maxPlayers;
    private string _connection;
    private const int TicketCheckMs = 1000;

    /// <summary>
    /// Number of players currently in the local backfill ticket.
    /// </summary>
    private int MatchPlayerCount =>
        _localBackfillTicket?.Properties?.MatchProperties?.Players?.Count ?? 0;

    /// <summary>
    /// The match properties currently stored in the local backfill ticket.
    /// </summary>
    private MatchProperties MatchProperties =>
        _localBackfillTicket?.Properties?.MatchProperties;

    /// <summary>
    /// Whether backfilling is currently active (loop is running).
    /// </summary>
    public bool IsBackfilling { get; private set; }

    /// <summary>
    /// Creates a backfill controller.
    /// </summary>
    /// <param name="connection">Server connection string (ip:port).</param>
    /// <param name="queueName">Matchmaking queue name to backfill.</param>
    /// <param name="matchmakerPayloadProperties">The initial match properties
    ///   from the allocation payload (teams, players, region, backfillTicketId).</param>
    /// <param name="maxPlayers">Maximum players for this match.</param>
    public MatchplayBackfiller(
        string connection,
        string queueName,
        MatchProperties matchmakerPayloadProperties,
        int maxPlayers)
    {
        _maxPlayers = maxPlayers;
        _connection = connection;

        // Create an initial BackfillTicket from the matchmaker payload properties.
        var backfillProperties = new BackfillTicketProperties(matchmakerPayloadProperties);

        _localBackfillTicket = new BackfillTicket(
            id: matchmakerPayloadProperties?.BackfillTicketId,
            connection: connection,
            properties: backfillProperties
        );
    }

    /// <summary>
    /// Starts the backfilling process. Creates a backfill ticket if one does
    /// not already exist, then begins the approve/update loop.
    /// </summary>
    public async Task BeginBackfilling()
    {
        if (IsBackfilling)
        {
            Debug.LogWarning("[Backfill] Already backfilling, no need to start another.");
            return;
        }

        Debug.Log($"[Backfill] Starting backfill. Current: {MatchPlayerCount}/{_maxPlayers}");

        // Create a new backfill ticket if we don't have one yet.
        if (string.IsNullOrEmpty(_localBackfillTicket?.Id))
        {
            var options = new CreateBackfillTicketOptions(
                queueName: null,  // set below
                connection: _connection,
                properties: _localBackfillTicket?.Properties
            )
            {
                QueueName = null, // Will use properties
                Properties = _localBackfillTicket?.Properties
            };

            // The CreateBackfillTicketOptions now expects connection OR ConnectionDetails.
            // We provide the connection string directly.
            string createdId;
            try
            {
                createdId = await MatchmakerService.Instance.CreateBackfillTicketAsync(
                    new CreateBackfillTicketOptions
                    {
                        QueueName = options.QueueName,
                        Connection = _connection,
                        Properties = _localBackfillTicket?.Properties
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backfill] Failed to create backfill ticket: {ex.Message}");
                return;
            }

            _localBackfillTicket.Id = createdId;
        }

        IsBackfilling = true;

        // Fire-and-forget the approve/update loop.
        _ = BackfillLoopAsync();
    }

    /// <summary>
    /// Adds a player to the local backfill ticket data. The next approval
    /// cycle will upload this change.
    /// </summary>
    public void AddPlayerToMatch(UserData userData)
    {
        if (!IsBackfilling)
        {
            Debug.LogWarning("[Backfill] Can't add users before backfilling has started.");
            return;
        }

        if (BackfillTicketIsValid())
        {
            if (GetPlayerById(userData.userAuthId) != null)
            {
                Debug.LogWarning($"[Backfill] User '{userData.userName}' ({userData.userAuthId}) already in backfill. Ignoring.");
                return;
            }
        }

        // Create a matchmaker Player with the auth ID as the player ID.
        // CustomData holds the player's game preferences / display name.
        var matchmakerPlayer = new MatchmakerPlayer(userData.userAuthId, new
        {
            username = userData.userName,
            preferences = userData.userGamePreferences
        });

        if (!BackfillTicketIsValid())
        {
            Debug.LogWarning("[Backfill] Local backfill ticket is not valid. Cannot add player.");
            return;
        }

        var mp = MatchProperties;
        if (mp == null) return;

        mp.Players.Add(matchmakerPlayer);

        // If there's at least one team, add the player to the first team.
        if (mp.Teams != null && mp.Teams.Count > 0)
        {
            mp.Teams[0].PlayerIds.Add(matchmakerPlayer.Id);
        }

        _localDataDirty = true;
    }

    /// <summary>
    /// Removes a player from the local backfill ticket by user ID.
    /// Returns the remaining player count.
    /// </summary>
    public int RemovePlayerFromMatch(string userId)
    {
        if (!BackfillTicketIsValid())
            return MatchPlayerCount;

        var playerToRemove = GetPlayerById(userId);
        if (playerToRemove == null)
        {
            Debug.LogWarning($"[Backfill] No user by ID '{userId}' in local backfill data.");
            return MatchPlayerCount;
        }

        var mp = MatchProperties;
        mp?.Players.Remove(playerToRemove);

        if (mp?.Teams != null && mp.Teams.Count > 0)
        {
            foreach (var team in mp.Teams)
            {
                team.PlayerIds.Remove(userId);
            }
        }

        _localDataDirty = true;
        return MatchPlayerCount;
    }

    /// <summary>
    /// Checks whether the match still needs more players.
    /// </summary>
    public bool NeedsPlayers()
    {
        return MatchPlayerCount < _maxPlayers;
    }

    /// <summary>
    /// Stops backfilling and deletes the backfill ticket.
    /// </summary>
    public async Task StopBackfill()
    {
        if (!IsBackfilling)
        {
            Debug.LogError("[Backfill] Can't stop backfilling before it starts.");
            return;
        }

        IsBackfilling = false;

        if (!string.IsNullOrEmpty(_localBackfillTicket?.Id))
        {
            try
            {
                await MatchmakerService.Instance.DeleteBackfillTicketAsync(_localBackfillTicket.Id);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backfill] Failed to delete backfill ticket: {ex.Message}");
            }
        }

        _localBackfillTicket.Id = null;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds a player in the local backfill ticket by their auth/user ID.
    /// </summary>
    private MatchmakerPlayer GetPlayerById(string userId)
    {
        if (!BackfillTicketIsValid())
            return null;

        return MatchProperties?.Players?.FirstOrDefault(p => p.Id == userId);
    }

    /// <summary>
    /// Periodic approve/update loop. Runs while IsBackfilling is true.
    /// </summary>
    private async Task BackfillLoopAsync()
    {
        while (IsBackfilling)
        {
            try
            {
                if (_localDataDirty && _localBackfillTicket != null)
                {
                    // Push local changes to the matchmaker.
                    await MatchmakerService.Instance.UpdateBackfillTicketAsync(
                        _localBackfillTicket.Id, _localBackfillTicket);
                    _localDataDirty = false;
                }
                else if (_localBackfillTicket != null)
                {
                    // Approve to receive new proposed players.
                    _localBackfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(
                        _localBackfillTicket.Id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Backfill] Loop error: {ex.Message}");
            }

            if (!NeedsPlayers())
            {
                Debug.Log("[Backfill] Match is full. Stopping backfill.");
                await StopBackfill();
                break;
            }

            await Task.Delay(TicketCheckMs);
        }
    }

    /// <summary>
    /// Validates that the local backfill ticket is in a usable state.
    /// </summary>
    private bool BackfillTicketIsValid()
    {
        return _localBackfillTicket?.Properties?.MatchProperties?.Players != null;
    }

    public void Dispose()
    {
        _ = StopBackfill();
    }
}
