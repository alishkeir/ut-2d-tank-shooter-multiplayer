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
using System.Collections;
using TMPro;
using UnityEngine;

public class MainMenu : MonoBehaviour
{

    [SerializeField] private TMP_InputField joinCodeField;
    [SerializeField] private TMP_Text findMatchButtonText;
    [SerializeField] private TMP_Text queueTimerText;
    [SerializeField] private TMP_Text queueStatusText;

    bool isMatchmaking = false;
    bool isCanceling = false;


    void Start()
    {

        if (ClientSingleton.Instance == null) return;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        queueStatusText.text = string.Empty;
        queueTimerText.text = string.Empty;
    }


    // Called by the "Host" button. Forwards to HostSingleton.GameManager.
    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }

    // Called by the "Join" button. Reads the join code and asks the client manager to connect.
    public async void StartClient()
    {
        await ClientSingleton.Instance.GameManager.StartClientAsync(joinCodeField.text);
    }

    public async void FindMatch()
    {

        if (isCanceling) return;

        if (isMatchmaking)
        {
            queueStatusText.text = "Cancelling...";
            isCanceling = true;
            // cancel matchmaking
            isCanceling = false;
            isMatchmaking = false;
            findMatchButtonText.text = "Find a Match";
            queueStatusText.text = string.Empty;
            return;
        }

        // start queue
        findMatchButtonText.text = "Cancel";
        queueStatusText.text = "Finding a match...";
        isMatchmaking = true;
    }

    // Called by the "Quit" button. Closes the application.
    public void ExitGame()
    {
        Application.Quit();
    }
}
