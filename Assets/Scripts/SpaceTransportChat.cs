using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lightweight multiplayer text chat for SpaceTransport.
/// The component creates its own HUD at runtime, so no scene references are required.
/// </summary>
public sealed class SpaceTransportChat : MonoBehaviour
{
    private const string MessageName = "SpaceTransport_ChatMessage";
    private const int MaxMessageLength = 160;
    private const int MaxVisibleMessages = 12;
    private const float SendCooldown = 0.75f;

    private static SpaceTransportChat instance;

    public static bool IsChatOpen { get; private set; }

    private readonly Dictionary<ulong, float> lastMessageTime = new Dictionary<ulong, float>();
    private readonly Queue<string> messageHistory = new Queue<string>();

    private NetworkManager networkManager;
    private bool handlerRegistered;
    private bool chatOpen;

    private Canvas canvas;
    private GameObject panel;
    private TMP_Text messagesText;
    private TMP_InputField inputField;
    private TMP_Text hintText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        SpaceTransportChat existing = FindFirstObjectByType<SpaceTransportChat>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject chatObject = new GameObject("SpaceTransportChat");
        instance = chatObject.AddComponent<SpaceTransportChat>();
        DontDestroyOnLoad(chatObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildHud();
    }

    private void Update()
    {
        NetworkManager current = NetworkManager.Singleton;

        if (networkManager != current)
        {
            networkManager = current;
            handlerRegistered = false;
        }

        if (networkManager != null && networkManager.IsListening && !handlerRegistered)
        {
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessageReceived);
            handlerRegistered = true;
        }

        bool connected = networkManager != null && networkManager.IsListening;
        if (canvas != null && canvas.gameObject.activeSelf != connected)
            canvas.gameObject.SetActive(connected);

        if (!connected)
        {
            chatOpen = false;
            IsChatOpen = false;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!chatOpen && keyboard.enterKey.wasPressedThisFrame)
            OpenChat();
        else if (chatOpen && keyboard.escapeKey.wasPressedThisFrame)
            CloseChat();
    }

    private void OnDestroy()
    {
        if (networkManager != null && handlerRegistered)
        {
            try
            {
                networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }
            catch (Exception)
            {
                // The NetworkManager may already have shut down.
            }
        }

        if (instance == this)
        {
            IsChatOpen = false;
            instance = null;
        }
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("Chat Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        panel = CreateRect("Chat Panel", canvasObject.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(20f, 20f);
        panelRect.sizeDelta = new Vector2(440f, 300f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.08f, 0.88f);

        TMP_Text header = CreateText("Chat Header", panel.transform, "SPACE TRANSPORT  /  CHAT", 18, FontStyles.Bold);
        SetAnchors(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -34f), new Vector2(-12f, -8f));

        messagesText = CreateText("Chat Messages", panel.transform, "Połączono. Naciśnij Enter, aby napisać.", 16, FontStyles.Normal);
        messagesText.alignment = TextAlignmentOptions.BottomLeft;
        messagesText.textWrappingMode = TextWrappingModes.Normal;
        SetAnchors(messagesText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 58f), new Vector2(-12f, -44f));

        hintText = CreateText("Chat Hint", panel.transform, "ENTER  —  napisz wiadomość", 14, FontStyles.Italic);
        hintText.color = new Color(0.72f, 0.82f, 0.92f, 0.9f);
        SetAnchors(hintText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 34f));

        GameObject inputObject = CreateRect("Chat Input", panel.transform);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        SetAnchors(inputRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(10f, 8f), new Vector2(-10f, 42f));

        Image inputBackground = inputObject.AddComponent<Image>();
        inputBackground.color = new Color(0.12f, 0.16f, 0.21f, 1f);

        inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = MaxMessageLength;
        inputField.richText = false;
        inputField.targetGraphic = inputBackground;
        inputField.textViewport = inputRect;

        TMP_Text inputText = CreateText("Input Text", inputObject.transform, string.Empty, 16, FontStyles.Normal);
        inputText.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(inputText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));

        TMP_Text placeholder = CreateText("Input Placeholder", inputObject.transform, "Wpisz wiadomość...", 16, FontStyles.Italic);
        placeholder.color = new Color(0.65f, 0.68f, 0.72f, 0.8f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        SetAnchors(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));

        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.onSubmit.AddListener(SubmitMessage);

        inputObject.SetActive(false);
        canvasObject.SetActive(false);
    }

    private void OpenChat()
    {
        chatOpen = true;
        IsChatOpen = true;
        inputField.gameObject.SetActive(true);
        hintText.gameObject.SetActive(false);
        inputField.text = string.Empty;
        inputField.ActivateInputField();
        inputField.Select();
    }

    private void CloseChat()
    {
        chatOpen = false;
        IsChatOpen = false;
        inputField.DeactivateInputField();
        inputField.gameObject.SetActive(false);
        hintText.gameObject.SetActive(true);
    }

    private void SubmitMessage(string rawMessage)
    {
        string message = SanitizeMessage(rawMessage);
        if (message.Length == 0)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
            return;
        }

        if (networkManager == null || !networkManager.IsListening)
        {
            CloseChat();
            return;
        }

        if (networkManager.IsServer)
        {
            // Host messages are processed directly; custom messages are for remote clients.
            ProcessChatMessage(networkManager.LocalClientId, message);
        }
        else
        {
            using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteValueSafe(message);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    MessageName,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        inputField.text = string.Empty;
        inputField.ActivateInputField();
    }

    private void OnMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (networkManager == null)
            return;

        // Clients receive the server's already-formatted broadcast.
        if (!networkManager.IsServer)
        {
            ReceiveBroadcast(senderClientId, reader);
            return;
        }

        // The server receives raw text from a client and validates it before broadcasting.
        string rawMessage;
        reader.ReadValueSafe(out rawMessage);
        ProcessChatMessage(senderClientId, rawMessage);
    }

    private void ProcessChatMessage(ulong senderClientId, string rawMessage)
    {
        string message = SanitizeMessage(rawMessage);
        if (message.Length == 0)
            return;

        float now = Time.unscaledTime;
        if (lastMessageTime.TryGetValue(senderClientId, out float previousTime) &&
            now - previousTime < SendCooldown)
            return;

        lastMessageTime[senderClientId] = now;

        string formatted = $"<color=#83CFFF>[{DateTime.Now:HH:mm}]</color> <b>Gracz {senderClientId}</b>: {EscapeRichText(message)}";
        using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteValueSafe(formatted);

            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                if (clientId == networkManager.LocalClientId)
                {
                    AddMessage(formatted);
                    continue;
                }

                networkManager.CustomMessagingManager.SendNamedMessage(
                    MessageName,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }
    }

    private void ReceiveBroadcast(ulong senderClientId, FastBufferReader reader)
    {
        string formatted;
        reader.ReadValueSafe(out formatted);
        AddMessage(formatted);
    }

    private void AddMessage(string message)
    {
        messageHistory.Enqueue(message);
        while (messageHistory.Count > MaxVisibleMessages)
            messageHistory.Dequeue();

        if (messagesText != null)
            messagesText.text = string.Join("\n", messageHistory);
    }

    private static string SanitizeMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder(Mathf.Min(value.Length, MaxMessageLength));
        foreach (char character in value)
        {
            if (!char.IsControl(character))
                builder.Append(character);

            if (builder.Length >= MaxMessageLength)
                break;
        }

        return builder.ToString().Trim();
    }

    private static string EscapeRichText(string value)
    {
        return value.Replace("<", "‹").Replace(">", "›");
    }

    private GameObject CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private TMP_Text CreateText(string objectName, Transform parent, string value, float fontSize, FontStyles style)
    {
        GameObject go = CreateRect(objectName, parent);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
