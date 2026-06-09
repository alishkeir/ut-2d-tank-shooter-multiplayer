// PlayerNameDisplay
// -----------------
// A small UI helper that renders a Player's PlayerName above (or near) them
// in world space. Subscribes to the network-synced PlayerName value and
// updates a TMP_Text component whenever it changes.
//
// Setup:
// - Place this on a world-space Canvas (typically parented to the player tank).
// - Assign the Player reference and the TMP_Text in the inspector.
using TMPro;
using Unity.Collections;
using UnityEngine;

public class PlayerNameDisplay : MonoBehaviour
{

    [SerializeField] private Player player;             // The networked player whose name we are showing.
    [SerializeField] private TMP_Text playerNameText;  // The text element that will display the name.

    private void Start()
    {
        // Set the initial value (in case the name was set before we spawned).
        HandlePlayerNameChanged(string.Empty, player.PlayerName.Value);
        // Subscribe to future updates.
        player.PlayerName.OnValueChanged += HandlePlayerNameChanged;
    }

    // Update the visible name whenever the server changes PlayerName.
    private void HandlePlayerNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        playerNameText.text = newValue.ToString();
    }

    private void OnDestroy()
    {
        // Avoid dangling subscriptions if the object is destroyed before the player.
        player.PlayerName.OnValueChanged -= HandlePlayerNameChanged;
    }

}
