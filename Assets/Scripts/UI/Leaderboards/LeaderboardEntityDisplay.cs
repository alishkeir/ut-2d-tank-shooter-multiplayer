using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LeaderboardEntityDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text displayText;
    [SerializeField] Color myColor = Color.green;

    private FixedString32Bytes playerName;

    public ulong ClientId { get; private set; }
    public int Coins { get; private set; }



    void Start()
    {
        UpdateText();
    }


    void Update()
    {

    }


    public void Initialise(ulong clientId, FixedString32Bytes name, int coins)
    {
        ClientId = clientId;
        playerName = name;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            displayText.color = myColor;
        }

        UpdateCoins(coins);

    }


    public void UpdateText()
    {
        displayText.text = $"{transform.GetSiblingIndex() + 1}. {playerName} - {Coins} ";
    }


    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }
}
