// LobbiesList
// -----------
// UI panel that shows the list of public lobbies available to join.
//
// Behavior:
// - When the panel is enabled (OnEnable), it refreshes the list by querying
//   the Unity Lobby service for up to 10 lobbies that:
//     * Have at least one open slot (AvailableSlots > 0).
//     * Are not locked/private.
// - For each returned lobby, a LobbyItem prefab is instantiated under
//   `lobbyItemParent` to render the lobby's name + player count.
// - Clicking "Join" on a LobbyItem calls JoinAsync(), which:
//     1. Joins the lobby by ID.
//     2. Reads the "RelayJoinCode" from the lobby data.
//     3. Tells ClientSingleton.GameManager we joined the lobby (so we leave
//        it cleanly on disconnect).
//     4. Calls StartClientAsync() with the join code to actually connect.
//
// Guards: prevents concurrent refreshes and joins via `isRefreshing`/`isJoining`.
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;


public class LobbiesList : MonoBehaviour
{

    [SerializeField] private LobbyItem lobbyItemPrefab;   // Prefab used to render each lobby in the list.
    [SerializeField] private Transform lobbyItemParent;   // Parent transform under which lobby items are spawned.
    [SerializeField] private MainMenu mainMenu;   // Parent transform under which lobby items are spawned.

    private bool isRefreshing = false; // Prevents multiple concurrent refreshes.




    // Refresh the list automatically when the panel is enabled.
    private void OnEnable()
    {
        RefreshList();
    }

    // Queries the Lobby service and rebuilds the UI list of lobbies.
    public async void RefreshList()
    {
        if (isRefreshing) { return; }
        isRefreshing = true;

        try
        {
            // Build a query: 10 lobbies, with available slots, and not locked.
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 10,
                Filters = new List<QueryFilter>()
            {
                new QueryFilter(field:QueryFilter.FieldOptions.AvailableSlots,op:QueryFilter.OpOptions.GT,value:"0"), // don't show lobbies that are full
                new QueryFilter(field:QueryFilter.FieldOptions.IsLocked,op:QueryFilter.OpOptions.EQ,value:"0") // don't show private lobbies (that need code to join)
            }
            };

            QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync(options);

            // Clear the existing list before re-populating.
            foreach (Transform child in lobbyItemParent)
            {
                Destroy(child.gameObject);
            }

            // Instantiate a LobbyItem for every returned lobby.
            foreach (Lobby lobby in lobbies.Results)
            {
                LobbyItem lobbyItem = Instantiate(lobbyItemPrefab, lobbyItemParent);
                lobbyItem.Initialize(this, lobby);

            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }




        isRefreshing = false;

    }


    public void JoinAsync(Lobby lobby)
    {
        mainMenu.JoinAsync(lobby);

    }

}
