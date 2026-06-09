// LeaderboardEntityDisplay
// ------------------------
// A single row in the Leaderboard. Shows a rank (sibling index + 1), a player
// name and the player's current coin count. The local player's row is
// recolored so it stands out from other players.
//
// Lifecycle:
// - Created by Leaderboard.HandleLeaderboardEntitiesChanged (Instantiate).
// - Initialise() is called immediately after creation to set the data.
// - UpdateCoins() is called whenever the server's NetworkList value changes.
// - UpdateText() is called after every sort/refresh to update the rank number.
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderboardEntityDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text displayText;       // The text element that shows "rank. name - coins".
    [SerializeField] Color myColor = Color.green; // Color used to highlight the local player's row.

    private FixedString32Bytes playerName;        // Cached display name (FixedString32 for network compat).

    public ulong ClientId { get; private set; }   // The owning client's NetworkManager id.
    public int Coins { get; private set; }        // The current coin count for this row.



    void Start()
    {
        // Refresh the text in case the prefab was enabled with stale data.
        UpdateText();
    }


    void Update()
    {

    }


    // Sets up the row with the player's data. Called by Leaderboard right after Instantiate.
    public void Initialise(ulong clientId, FixedString32Bytes name, int coins)
    {
        ClientId = clientId;
        playerName = name;

        // Highlight the local player's row.
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColor;
        }

        UpdateCoins(coins);

    }


    // Rebuilds the displayed text using the current sibling index as the rank.
    // Called every time the leaderboard re-sorts.
    public void UpdateText()
    {
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {playerName} - {Coins} ";
    }


    // Updates the cached coin count and refreshes the text.
    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }
}
