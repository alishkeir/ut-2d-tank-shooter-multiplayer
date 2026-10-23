// ApplicationController
// ---------------------
// Top-level bootstrapper. Lives on a "boot" GameObject in the very first scene.
//
// Responsibilities:
// - Decide whether we're running as a dedicated server (no graphics device) or a client.
// - Spawn the HostSingleton and ClientSingleton prefabs so they survive scene loads.
// - Authenticate the local player and (if successful) load the main menu scene.
//
// Place a single instance of this component in your Boot/Initialization scene
// and assign the HostSingleton / ClientSingleton prefabs in the inspector.
using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ApplicationController : MonoBehaviour
{

    [SerializeField] private ClientSingleton clientPrefab; // Prefab containing the ClientSingleton component.
    [SerializeField] private HostSingleton hostPrefab;     // Prefab containing the HostSingleton component.
    [SerializeField] private ServerSingleton serverPrefab;     // Prefab containing the ServerSingleton component.
    [SerializeField] private NetworkObject playerPrefab;     // Prefab containing the ServerSingleton component.

    ApplicationData appData;

    private const int SERVER_TARGET_FRAMERATE = 30;

    private const string GameSceneName = "Game";



    private async void Start()
    {
        // Survive scene loads so the singletons stay alive across the whole game.
        DontDestroyOnLoad(this);

        // Check if we are in dedicated server mode.
        // A dedicated server won't have a graphics device, which lets us detect it at runtime.
        await LaunchInMode(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null);
    }


    // Launches the appropriate role.
    // For now the dedicated server branch is empty (room for future dedicated-server logic).
    async Task LaunchInMode(bool isDedicatedServer)
    {


        if (isDedicatedServer)
        {

            Application.targetFrameRate = SERVER_TARGET_FRAMERATE;
            QualitySettings.vSyncCount = 0;

            Debug.Log($"Dedicated server target frame rate: {SERVER_TARGET_FRAMERATE}");

            appData = new ApplicationData();

            ServerSingleton serverSingleton = Instantiate(serverPrefab);

            StartCoroutine(LoadGameSceneAsync(serverSingleton));

            return;
        }
        else
        {

            // Spin up the local host (server + local client).
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.CreateHost(playerPrefab);

            // Spin up the client (which authenticates with Unity Services).
            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            bool isAuthenticated = await clientSingleton.CreateClient();

            // Only proceed to the main menu if authentication succeeded.
            if (isAuthenticated)
            {
                clientSingleton.GoToMainMenu();
            }
        }

    }

    private IEnumerator LoadGameSceneAsync(ServerSingleton serverSingleton)
    {
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Single);

        while (!asyncOperation.isDone)
        {
            yield return null;
        }

        // to wait for the server to be created before starting it.
        Task createServerTask = serverSingleton.CreateServer(playerPrefab);
        yield return new WaitUntil(() => createServerTask.IsCompleted);

        Task startServerTask = serverSingleton.GameManager.StartGameServerAsync();
        yield return new WaitUntil(() => startServerTask.IsCompleted);


    }

}
