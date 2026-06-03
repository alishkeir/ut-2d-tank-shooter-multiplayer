using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ChatUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject background;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentParent;
    [SerializeField] private TMP_Text messagePrefab;


    [Header("Settings")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private float inactiveAlpha = 0.25f;
    [SerializeField] private int maxMessages = 10;

    private bool chatOpen;

    private CanvasGroup backgroundGroup;

    private void Start()
    {
        backgroundGroup = background.GetComponent<CanvasGroup>();

        SetInactiveState();

        ChatManager.Instance.OnMessageReceived += AddMessage;
    }

    private void OnDestroy()
    {
        if (ChatManager.Instance != null)
            ChatManager.Instance.OnMessageReceived -= AddMessage;
    }

    private void Update()
    {
        if (!chatOpen)
        {
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                OpenChat();
            }

            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseChat();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            SubmitChat();
            return;
        }

        float wheel = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(wheel) > 0.01f)
        {
            scrollRect.verticalNormalizedPosition += wheel * 0.001f;
        }
    }

    private void OpenChat()
    {
        chatOpen = true;

        backgroundGroup.alpha = 1f;

        inputField.gameObject.SetActive(true);

        inputField.text = "";

        inputField.Select();
        inputField.ActivateInputField();
    }

    private void CloseChat()
    {
        chatOpen = false;

        inputField.text = "";

        EventSystem.current.SetSelectedGameObject(null);

        SetInactiveState();
    }

    private void SubmitChat()
    {
        string message = inputField.text.Trim();

        if (!string.IsNullOrEmpty(message))
        {
            ChatManager.Instance.SendChatMessageRpc(playerName, message);
        }

        CloseChat();
    }

    private void SetInactiveState()
    {
        backgroundGroup.alpha = inactiveAlpha;

        inputField.gameObject.SetActive(false);
    }

    private void AddMessage(string message)
    {
        TMP_Text msg = Instantiate(messagePrefab, contentParent);

        msg.text = message;

        Canvas.ForceUpdateCanvases();

        if (contentParent.childCount > maxMessages)
        {
            // remove oldest (top message)
            Destroy(contentParent.GetChild(0).gameObject);
        }

        scrollRect.verticalNormalizedPosition = 0f;
    }

    public bool IsChatOpen()
    {
        return chatOpen;
    }
}