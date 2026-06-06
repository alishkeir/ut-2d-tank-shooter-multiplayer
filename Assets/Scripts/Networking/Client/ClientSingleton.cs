using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientSingleton : MonoBehaviour
{

    private const string _MENU_SCENE = "Menu";

    private static ClientSingleton instance;

    public ClientGameManager GameManager { get; private set; }

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
        DontDestroyOnLoad(this);
    }


    public async Task<bool> CreateClient()
    {
        GameManager = new ClientGameManager();
        return await GameManager.InitAsync();
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(_MENU_SCENE);
    }

    private void OnDestroy()
    {
        GameManager?.Dispose();
    }


}
