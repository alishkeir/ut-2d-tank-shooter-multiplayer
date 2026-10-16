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
// UNITY 6 COMPAT: Removed unused "using System.Runtime.InteropServices.WindowsRuntime;"
// — it is Windows-only and breaks Linux dedicated-server builds.
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientSingleton : MonoBehaviour
{
    private static ClientSingleton instance;

    public ClientGameManager GameManager { get; private set; }

    // Name of the main menu scene. Must match the scene in Build Settings.
    private const string _MENU_SCENE = "Menu";


    public static ClientSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<ClientSingleton>();

            if (instance == null)
            {
                Debug.LogError("No ClientSingleton in the scene!");
                return null;
            }

            return instance;
        }
    }
    private void Start()
    {
        DontDestroyOnLoad(gameObject);


    }

    public async Task<bool> CreateClient()
    {
        GameManager = new ClientGameManager();

        return await GameManager.InitAsync();
    }

    void OnDestroy()
    {
        GameManager?.Dispose();
    }

    // Loads the main menu scene (only call this once the player is authenticated).
    public void GoToMainMenu()
    {
        SceneManager.LoadScene(_MENU_SCENE);
    }




}