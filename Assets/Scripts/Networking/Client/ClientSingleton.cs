// ClientSingleton
// ---------------
// A DontDestroyOnLoad MonoBehaviour singleton that owns the ClientGameManager
// for the duration of the application. Instantiated once at boot by
// ApplicationController.
//
// Access from anywhere:
//   ClientSingleton.Instance.GameManager
//
// Responsibilities:
// - Create and initialize a ClientGameManager (which authenticates with UGS).
// - GoToMainMenu() loads the main menu scene once auth has succeeded.
// - OnDestroy disposes the ClientGameManager (which leaves any joined lobby
//   and disconnects the network client cleanly).
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientSingleton : MonoBehaviour
{

    // Name of the main menu scene. Must match the scene in Build Settings.
    private const string _MENU_SCENE = "Menu";

    private static ClientSingleton instance;

    // The actual game-management object. Null until CreateClient() is called.
    public ClientGameManager GameManager { get; private set; }

    // Lazy singleton accessor. Looks up the instance in the scene if needed.
    public static ClientSingleton Instance
    {
        get
        {

            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<ClientSingleton>();

            if (instance == null)
            {
                Debug.LogError("ClientSingleton not found!");
                return null;
            }

            return instance;
        }

    }

    void Start()
    {
        // Keep this alive across scene loads so the GameManager survives.
        DontDestroyOnLoad(this);
    }


    // Creates the ClientGameManager and runs its async initialization.
    // Returns true if authentication succeeded and we are ready to proceed.
    public async Task<bool> CreateClient()
    {
        GameManager = new ClientGameManager();
        return await GameManager.InitAsync();
    }

    // Loads the main menu scene (only call this once the player is authenticated).
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(_MENU_SCENE);
    }

    private void OnDestroy()
    {
        // Clean up the client on shutdown.
        GameManager?.Dispose();
    }


}
