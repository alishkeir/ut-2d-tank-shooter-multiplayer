// MainMenu
// --------
// Buttons for the main menu scene. Wire these methods to UI Button OnClick
// events in the inspector.
//
// Buttons:
// - "Host"   -> calls StartHost()   which kicks off HostGameManager.StartHostAsync.
// - "Join"   -> calls StartClient() which reads the join code from the input
//               field and calls ClientGameManager.StartClientAsync.
// - "Quit"   -> calls ExitGame()    which closes the application.
using System;
using System.Collections;
using TMPro;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class MainMenu : MonoBehaviour
{

    [SerializeField] private TMP_InputField joinCodeField;
    [SerializeField] private TMP_Text findMatchButtonText;
    [SerializeField] private TMP_Text queueTimerText;
    [SerializeField] private TMP_Text queueStatusText;

    bool isMatchmaking = false;
    bool isCanceling = false;
    bool isBusy = false;

    float timeInQueue = 0f;

    void Start()
    {

        if (ClientSingleton.Instance == null) return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        queueStatusText.text = string.Empty;
        queueTimerText.text = string.Empty;
    }


    void Update()
    {
        if (isMatchmaking)
        {
            timeInQueue += Time.deltaTime;

            TimeSpan ts = TimeSpan.FromSeconds(timeInQueue);

            queueTimerText.text = string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
        }
    }


    // Called by the "Host" button. Forwards to HostSingleton.GameManager.
    public async void StartHost()
    {
        if (isBusy) return;
        isBusy = true;
        await HostSingleton.Instance.GameManager.StartHostAsync();
        isBusy = false;
    }

    // Called by the "Join" button. Reads the join code and asks the client manager to connect.
    public async void StartClient()
    {
        if (isBusy) return;
        isBusy = true;
        await ClientSingleton.Instance.GameManager.StartClientAsync(joinCodeField.text);
        isBusy = false;
    }

    public async void FindMatch()
    {
        if (isCanceling) return;

        if (isMatchmaking)
        {
            queueStatusText.text = "Cancelling...";
            isCanceling = true;

            await ClientSingleton.Instance.GameManager.CancelMatchmaking();

            isCanceling = false;
            isMatchmaking = false;
            isBusy = false;
            findMatchButtonText.text = "Find a Match";
            queueStatusText.text = string.Empty;
            queueTimerText.text = string.Empty;
            return;
        }

        if (isBusy) return;

        ClientSingleton.Instance.GameManager.MatchMakeAsync(OnMatchMade);
        timeInQueue = 0f;
        findMatchButtonText.text = "Cancel";
        queueStatusText.text = "Finding a match...";
        isMatchmaking = true;
        isBusy = true;
    }

    // Joins the given lobby and connects to its Relay server.
    public async void JoinAsync(Lobby lobby)
    {
        if (isBusy) { return; }
        isBusy = true;

        try
        {
            // Join the lobby (so we're counted as a member) and read the Relay join code.
            Lobby joiningLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
            string joinCode = joiningLobby.Data["JoinCode"].Value;

            // Remember the lobby ID so we can leave it cleanly on disconnect.
            ClientSingleton.Instance.GameManager.SetJoinedLobbyId(lobby.Id);

            // Connect to the host's Relay server using the join code.
            await ClientSingleton.Instance.GameManager.StartClientAsync(joinCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
        isBusy = false;
    }

    void OnMatchMade(MatchmakerPollingResult result)
    {
        switch (result)
        {
            case MatchmakerPollingResult.Success:
                queueStatusText.text = "Connecting...";
                break;

            case MatchmakerPollingResult.TicketCreationError:
                HandleMatchmakingError("TicketCreationError");
                break;

            case MatchmakerPollingResult.TicketCancellationError:
                // HandleMatchmakingError("TicketCancellationError");
                HandleMatchmakingError(string.Empty);
                break;

            case MatchmakerPollingResult.TicketRetrievalError:
                HandleMatchmakingError("TicketRetrievalError");
                break;

            case MatchmakerPollingResult.MatchAssignmentError:
                HandleMatchmakingError("MatchAssignmentError");
                break;
        }
    }

    void HandleMatchmakingError(string errorMessage = "")
    {
        findMatchButtonText.text = "Find a Match";
        queueTimerText.text = string.Empty;
        queueStatusText.text = errorMessage;
        isMatchmaking = false;
        isCanceling = false;
        isBusy = false;
    }

    // Called by the "Quit" button. Closes the application.
    public void ExitGame()
    {
        Application.Quit();
    }
}
