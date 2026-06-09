using TMPro;
using Unity.Collections;
using UnityEngine;

public class LeaderboardEntityDisplay : MonoBehaviour
{
    [SerializeField] TMP_Text displayText;

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
        UpdateCoins(coins);
    }


    private void UpdateText()
    {
        displayText.text = $"1. {playerName} - {Coins} ";
    }


    public void UpdateCoins(int coins)
    {
        Coins = coins;
        UpdateText();
    }
}
