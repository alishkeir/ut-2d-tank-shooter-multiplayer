// LobbyItem
// ---------
// A single row in the LobbiesList. Renders a lobby's name and current
// player count, and provides a "Join" button that forwards to the parent
// LobbiesList's JoinAsync() method.
//
// Setup:
// - Place this on a prefab with two TMP_Text elements (name + player count).
// - Wire the "Join" button's OnClick to the Join() method.
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;   // The label that shows the lobby name.
    [SerializeField] private TMP_Text playerCountText; // The label that shows "current/max" players.

    private LobbiesList lobbiesList; // Reference to the parent list (so we can ask it to join).
    private Lobby lobby;             // The lobby data this row represents.


    // Populates the row. Called by LobbiesList.RefreshList right after Instantiate.
    public void Initialize(LobbiesList lobbiesList, Lobby lobby)
    {

        this.lobbiesList = lobbiesList;
        this.lobby = lobby;

        lobbyNameText.text = lobby.Name;

        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
    }

    // Called by the row's "Join" button. Forwards to LobbiesList.JoinAsync.
    public void Join()
    {
        lobbiesList.JoinAsync(lobby);
    }
}
