// ServerSingleton
// ---------------
// A DontDestroyOnLoad MonoBehaviour singleton that owns the ServerGameManager
// for the duration of the application. Instantiated once at boot by
// ApplicationController when running as a dedicated server.
//
// Access from anywhere:
//   ServerSingleton.Instance.GameManager
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;

public class ServerSingleton : MonoBehaviour
{
    private static ServerSingleton instance;

    [Header("Dedicated Server (formerly ServerBootstrap)")]
    [SerializeField] private string localServerId = "oracle-1";
    [SerializeField] private bool useRelay = true;
    [Tooltip("At least the queue pool's maximum player count.")]
    [SerializeField] private int maxPlayers = 1;
    [SerializeField] private string gameSceneName = "Game";

    public ServerGameManager GameManager { get; private set; }

    public static ServerSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<ServerSingleton>();

            if (instance == null)
            {
                Debug.LogError("No ServerSingleton in the scene!");
                return null;
            }

            return instance;
        }
    }

    private void Start()
    {
        DontDestroyOnLoad(this);
    }

    // Initializes Unity Services and creates the ServerGameManager. Call once during boot.
    public async Task CreateServer()
    {

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null. Ensure the NetworkManager exists in the boot scene.");
            return;
        }

        Debug.Log($"NetworkManager: {NetworkManager.Singleton}");
        Debug.Log($"UnityTransport: {NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()}");

        var manager = NetworkManager.Singleton;
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

        Debug.Log($"Transport component enabled: {transport.enabled}");
        Debug.Log($"NetworkConfig exists: {NetworkManager.Singleton.NetworkConfig != null}");

        if (manager.NetworkConfig != null)
            Debug.Log($"Transport assigned: {manager.NetworkConfig.NetworkTransport != null}");

        await UnityServices.InitializeAsync();

        GameManager = new ServerGameManager(
            NetworkManager.Singleton,
            localServerId,
            useRelay,
            maxPlayers,
            gameSceneName
        );
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }
}