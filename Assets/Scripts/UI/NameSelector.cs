// NameSelector
// ------------
// The first screen the player sees. Lets them type a display name and then
// advances to the next scene (the main menu).
//
// Behavior:
// - On Start, if we are running on a dedicated server, skip this scene entirely
//   (it doesn't make sense to ask a server for a player name).
// - Pre-fills the input with the previously saved name (PlayerPrefs).
// - The "Connect" button is only interactable when the name length is within
//   [minNameLength, maxNameLength].
// - On Connect, the name is saved to PlayerPrefs and the next scene is loaded.
//
// The PlayerNameKey constant is exposed so other systems (e.g. HostGameManager)
// can read the saved name from PlayerPrefs without duplicating the key.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NameSelector : MonoBehaviour
{

    [SerializeField] private TMP_InputField nameInputField; // The text input the user types their name into.
    [SerializeField] private Button connectButton;          // The button that confirms the name and advances.

    [Range(1, 5)]
    [SerializeField] private int minNameLength = 1;   // Minimum allowed name length (button stays disabled below this).
    [Range(6, 20)]
    [SerializeField] private int maxNameLength = 20;  // Maximum allowed name length (button stays disabled above this).

    // The PlayerPrefs key used to persist the chosen name across sessions.
    public const string PlayerNameKey = "PlayerName";

    private void Start()
    {

        // On a dedicated server (no graphics device), skip this UI scene.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            return;
        }

        // Pre-fill the field with the previously saved name (or empty if none).
        nameInputField.text = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
        HandleNameChange();
    }


    // Called every time the user changes the name (wire to TMP_InputField OnValueChanged).
    // Enables or disables the Connect button based on the configured length range.
    public void HandleNameChange()
    {
        connectButton.interactable = (nameInputField.text.Length >= minNameLength) && (nameInputField.text.Length <= maxNameLength);
    }

    // Called by the Connect button. Saves the name and loads the next scene (Main Menu).
    public void Connect()
    {
        PlayerPrefs.SetString(PlayerNameKey, nameInputField.text);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

}
