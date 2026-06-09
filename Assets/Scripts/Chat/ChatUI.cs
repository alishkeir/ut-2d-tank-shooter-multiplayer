// ChatUI
// ------
// Local chat UI (client-side only).
//
// Controls:
// - Press T to open the chat input.
// - Press ESC to close the chat input.
// - Press ENTER to submit a message.
// - Mouse wheel to scroll the chat list (when chat is closed).
//
// Subscribes to ChatManager.Instance.OnMessageReceived and spawns a UI text line
// for each new message. Trims the visible list to `maxMessages` to keep it tidy.
//
// Required setup:
// - Place this on a Canvas with a "background" GameObject that has a CanvasGroup.
// - Wire the SerializeField references (input field, scroll rect, message prefab).
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ChatUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject background;       // The panel behind the chat (must have a CanvasGroup component).
    [SerializeField] private TMP_InputField inputField;   // The input field users type into.
    [SerializeField] private ScrollRect scrollRect;       // The scroll view that holds the chat history.
    [SerializeField] private Transform contentParent;     // The content transform inside the scroll view (where messages are parented).
    [SerializeField] private TMP_Text messagePrefab;      // A TMP_Text prefab used to instantiate each new chat line.


    [Header("Settings")]
    [SerializeField] private string playerName = "Player"; // Display name attached to messages from this client.
    [SerializeField] private float inactiveAlpha = 0.25f; // Background opacity when the chat is closed.
    [SerializeField] private int maxMessages = 10;        // Maximum number of messages kept visible (oldest are removed).

    private bool chatOpen;          // Tracks whether the chat input is currently active.

    private CanvasGroup backgroundGroup; // Cached CanvasGroup for fading the background in/out.

    private void Start()
    {
        // Cache the CanvasGroup used to fade the background.
        backgroundGroup = background.GetComponent<CanvasGroup>();

        // Start in the "closed" visual state.
        SetInactiveState();

        // Begin listening for new chat messages.
        ChatManager.Instance.OnMessageReceived += AddMessage;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks or missing-reference errors.
        if (ChatManager.Instance != null)
            ChatManager.Instance.OnMessageReceived -= AddMessage;
    }

    private void Update()
    {
        // When the chat is closed: T opens it.
        if (!chatOpen)
        {
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                OpenChat();
            }

            return;
        }

        // When the chat is open: ESC closes it, ENTER submits, scroll wheel scrolls history.
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

        // Apply scroll wheel input to the scroll rect (scaled to feel comfortable).
        if (Mathf.Abs(wheel) > 0.01f)
        {
            scrollRect.verticalNormalizedPosition += wheel * 0.001f;
        }
    }

    private void OpenChat()
    {
        chatOpen = true;

        // Make the background fully visible while typing.
        backgroundGroup.alpha = 1f;

        // Show and focus the input field.
        inputField.gameObject.SetActive(true);

        inputField.text = "";

        inputField.Select();
        inputField.ActivateInputField();
    }

    private void CloseChat()
    {
        chatOpen = false;

        inputField.text = "";

        // Deselect to avoid keeping the input field highlighted.
        EventSystem.current.SetSelectedGameObject(null);

        SetInactiveState();
    }

    private void SubmitChat()
    {
        string message = inputField.text.Trim();

        // Only send non-empty messages.
        if (!string.IsNullOrEmpty(message))
        {
            ChatManager.Instance.SendChatMessageRpc(playerName, message);
        }

        CloseChat();
    }

    private void SetInactiveState()
    {
        // Visually fade the background and hide the input field.
        backgroundGroup.alpha = inactiveAlpha;

        inputField.gameObject.SetActive(false);
    }

    private void AddMessage(string message)
    {
        // Spawn a new message line from the prefab and set its text.
        TMP_Text msg = Instantiate(messagePrefab, contentParent);

        msg.text = message;

        // Force the canvas to update so we can read childCount reliably.
        Canvas.ForceUpdateCanvases();

        if (contentParent.childCount > maxMessages)
        {
            // Remove the oldest (top) message to keep the list bounded.
            Destroy(contentParent.GetChild(0).gameObject);
        }

        // Auto-scroll to the bottom (newest message).
        scrollRect.verticalNormalizedPosition = 0f;
    }

    // Public helper for other systems to query whether the chat input is open
    // (useful to prevent gameplay input from firing while typing).
    public bool IsChatOpen()
    {
        return chatOpen;
    }
}
