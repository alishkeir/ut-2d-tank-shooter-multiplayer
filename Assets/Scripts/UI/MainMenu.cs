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
using TMPro;
using UnityEngine;

public class MainMenu : MonoBehaviour
{

    [SerializeField] private TMP_InputField joinCodeField; // Input field where the user pastes the host's join code.


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

    // Called by the "Quit" button. Closes the application.
    public void ExitGame()
    {
        Application.Quit();
    }
}
