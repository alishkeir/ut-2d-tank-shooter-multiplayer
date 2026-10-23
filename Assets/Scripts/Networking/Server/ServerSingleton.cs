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
    [Tooltip("Must be at least the matchmaker queue pool's maximum player count (the rules currently require 20).")]
    [SerializeField] private int maxPlayers = 20;
    [SerializeField] private string gameSceneName = "Game";
    [Tooltip("Relay region id (for example the one closest to the server VM). Leave empty to let the SDK run QoS measurements to pick one, which adds a delay to every match.")]
    [SerializeField] private string relayRegion = "";
    [Tooltip("Lets the matchmaker refill the match when players leave. Needs the 'Matchmaker Backfill Admin' role on the service account, otherwise it fails with a 403.")]
    [SerializeField] private bool enableBackfill = false;

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
    public async Task CreateServer(NetworkObject playerPrefab)
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
            gameSceneName,
            playerPrefab,
            enableBackfill,
            relayRegion
        );
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }
}