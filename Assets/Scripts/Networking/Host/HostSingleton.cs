// HostSingleton
// -------------
// A DontDestroyOnLoad MonoBehaviour singleton that owns the HostGameManager
// for the duration of the application. Instantiated once at boot by
// ApplicationController.
//
// Access from anywhere:
//   HostSingleton.Instance.GameManager
//
// Responsibilities:
// - Lazily locate or return the singleton instance.
// - Create a HostGameManager on demand via CreateHost().
// - OnDestroy disposes the HostGameManager (which deletes the lobby and the
//   NetworkServer cleanly).
using Unity.Netcode;
using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    private static HostSingleton instance;

    // The actual game-management object. Null until CreateHost() is called.
    public HostGameManager GameManager { get; private set; }

    // Lazy singleton accessor. Looks up the instance in the scene if needed.
    public static HostSingleton Instance
    {
        get
        {
            if (instance != null) { return instance; }

            instance = FindAnyObjectByType<HostSingleton>();

            if (instance == null)
            {
                Debug.LogError("No HostSingleton in the scene!");
                return null;
            }

            return instance;
        }
    }

    private void Start()
    {
        // Keep this alive across scene loads so the GameManager survives.
        DontDestroyOnLoad(this);
    }

    // Instantiates a new HostGameManager. Call this once during boot.
    public void CreateHost(NetworkObject playerPrefab)
    {
        GameManager = new HostGameManager(playerPrefab);
    }

    private void OnDestroy()
    {
        // Clean up the host on shutdown.
        GameManager?.Dispose();
    }
}
