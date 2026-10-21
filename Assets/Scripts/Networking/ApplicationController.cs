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
using System.Threading.Tasks;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{

    [SerializeField] private ClientSingleton clientPrefab; // Prefab containing the ClientSingleton component.
    [SerializeField] private HostSingleton hostPrefab;     // Prefab containing the HostSingleton component.
    [SerializeField] private ServerSingleton serverPrefab;     // Prefab containing the ServerSingleton component.




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
            ServerSingleton serverSingleton = Instantiate(serverPrefab);

            await serverSingleton.CreateServer();

            await serverSingleton.GameManager.StartGameServerAsync();

            return;
        }
        else
        {

            // Spin up the local host (server + local client).
            HostSingleton hostSingleton = Instantiate(hostPrefab);
            hostSingleton.CreateHost();

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

}
