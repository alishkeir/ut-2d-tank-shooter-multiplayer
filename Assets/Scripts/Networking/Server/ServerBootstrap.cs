using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

#if UNITY_SERVER
using Unity.Services.Authentication.Server;
#endif
public class ServerBootstrap : MonoBehaviour
{

#if UNITY_SERVER
    // Must match the module's constants.
    const string AllocatorPlayerId = "LocalServerAllocator00000000";
    const string PendingMatchIdKey = "pendingMatchId";
    const string ControlSessionPrefix = "local-";

    // Environment variables keep the credentials out of source control for local testing only; they are not the
    // suggested way to handle secrets. Load them from your secret store if you have one.
    const string ServiceAccountKeyIdVariable = "LOCAL_SERVER_SERVICE_ACCOUNT_KEY_ID";
    const string ServiceAccountSecretVariable = "LOCAL_SERVER_SERVICE_ACCOUNT_SECRET";

    [SerializeField] string m_LocalServerId = "oracle-1";
    [SerializeField] bool m_UseRelay = true;
    [Tooltip("At least the queue pool's maximum player count.")]
    [SerializeField] int m_MaxPlayers = 1;

    IServerSession m_ControlSession;
    IServerSession m_MatchSession;
    string m_LastHandledMatchId;


    async void Start()
    {

        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            return;

        try
        {
            await UnityServices.InitializeAsync();


            var keyId = Environment.GetEnvironmentVariable(ServiceAccountKeyIdVariable)?.Trim().Trim('"', '\'');
            var secret = Environment.GetEnvironmentVariable(ServiceAccountSecretVariable)?.Trim().Trim('"', '\'');

            Debug.Log($"Key ID length: {keyId?.Length ?? 0}, Secret length: {secret?.Length ?? 0}");

            await ServerAuthenticationService.Instance.SignInWithServiceAccountAsync(keyId, secret);


            // The control session only carries the match id, so it needs no network.
            m_ControlSession = await MultiplayerServerService.Instance.CreateSessionAsync(
                ControlSessionPrefix + m_LocalServerId,
                new SessionOptions { Name = ControlSessionPrefix + m_LocalServerId, IsPrivate = true, MaxPlayers = 2 });

            // The allocator joins once, then updates its player data for every later match.
            m_ControlSession.PlayerJoined += _ => OnControlSessionChanged();
            m_ControlSession.PlayerPropertiesChanged += OnControlSessionChanged;
            Debug.Log($"Control session '{m_ControlSession.Id}' ready.");
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
    }

    void OnControlSessionChanged() => _ = CreatePendingMatchSessionAsync();

    async Task CreatePendingMatchSessionAsync()
    {
        var allocator = m_ControlSession.Players.FirstOrDefault(player => player.Id == AllocatorPlayerId);
        if (allocator == null ||
            !allocator.Properties.TryGetValue(PendingMatchIdKey, out var pendingMatchId) ||
            string.IsNullOrEmpty(pendingMatchId.Value) ||
            pendingMatchId.Value == m_LastHandledMatchId)
        {
            return;
        }

        var matchId = pendingMatchId.Value;
        m_LastHandledMatchId = matchId;

        try
        {
            // One NetworkManager runs one match at a time, so the next match replaces the current one.
            if (m_MatchSession != null)
            {
                var previousMatchSession = m_MatchSession;
                m_MatchSession = null;
                await previousMatchSession.DeleteAsync();
            }

            // Don't set the same SessionOptions.Type as the control session: the SDK keeps one session per type.
            var options = new SessionOptions { MaxPlayers = m_MaxPlayers };
            options = m_UseRelay ? options.WithRelayNetwork() : options.WithDirectNetwork();

            m_MatchSession = await MultiplayerServerService.Instance.CreateMatchSessionAsync(matchId, options);
            m_MatchSession.PlayerJoined += playerId => Debug.Log($"Player joined match {matchId}: {playerId}");
            Debug.Log($"Match session '{m_MatchSession.Id}' created ({(m_UseRelay ? "Relay" : "Direct")}).");
        }
        catch (Exception e)
        {
            Debug.LogException(Unwrap(e));
        }
    }

    // The server session APIs surface failures wrapped in AggregateException.
    static Exception Unwrap(Exception exception) =>
        exception is AggregateException aggregate ? aggregate.Flatten().InnerException ?? exception : exception;

#endif
}