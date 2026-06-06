using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NameSelector : MonoBehaviour
{

    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button connectButton;

    [Range(1, 5)]
    [SerializeField] private int minNameLength = 1;
    [Range(6, 20)]
    [SerializeField] private int maxNameLength = 20;

    public const string PlayerNameKey = "PlayerName";

    private void Start()
    {

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            return;
        }

        nameInputField.text = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
        HandleNameChange();
    }


    public void HandleNameChange()
    {
        connectButton.interactable = (nameInputField.text.Length >= minNameLength) && (nameInputField.text.Length <= maxNameLength);
    }

    public void Connect()
    {
        PlayerPrefs.SetString(PlayerNameKey, nameInputField.text);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

}
