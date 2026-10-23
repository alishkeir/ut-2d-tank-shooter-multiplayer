using TMPro;
using Unity.Netcode;
using UnityEngine;


public class GameHUD : MonoBehaviour
{

    [SerializeField] private TMP_Text lobbyJoinCodeText;

    public async void LeaveGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            await HostSingleton.Instance.GameManager.ShutdownAsync();
        }

        ClientSingleton.Instance.GameManager.Disconnect();
    }

    void Start()
    {

        lobbyJoinCodeText.gameObject.SetActive(true);

        if (NetworkManager.Singleton.IsHost)
        {
            Debug.Log(HostSingleton.Instance.GameManager.JoinCode);
            lobbyJoinCodeText.text = HostSingleton.Instance.GameManager.JoinCode;
        }
        else
        {
            Debug.Log(ClientSingleton.Instance.GameManager.JoinCode);
            lobbyJoinCodeText.text = ClientSingleton.Instance.GameManager.JoinCode;
        }
    }
}
