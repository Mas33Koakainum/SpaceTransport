using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Self-contained Among Us-style wire task. Add this component to any active GameObject
/// or let its runtime bootstrap create it. The UI is built entirely at runtime.
/// </summary>
public sealed class WireConnectionMinigame : MonoBehaviour
{
    private static WireConnectionMinigame instance;
    private static readonly Color[] WireColors =
    {
        new Color(0.94f, 0.20f, 0.22f),
        new Color(1.00f, 0.72f, 0.16f),
        new Color(0.20f, 0.58f, 0.96f),
        new Color(0.22f, 0.78f, 0.48f),
        new Color(0.72f, 0.38f, 0.91f)
    };

    [SerializeField] private bool showOnStart = true;
    [SerializeField] private Key openKey = Key.F3;

    private readonly bool[] connected = new bool[5];
    private readonly List<RectTransform> targetRects = new List<RectTransform>();
    private readonly List<int> targetColors = new List<int>();
    private readonly List<RectTransform> wireRects = new List<RectTransform>();

    private Canvas canvas;
    private RectTransform board;
    private TMP_Text statusText;
    private TMP_Text progressText;
    private GameObject overlay;
    private int activeDrag = -1;
    private RectTransform previewLine;
    private bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        WireConnectionMinigame existing = FindFirstObjectByType<WireConnectionMinigame>();
        if (existing != null) { instance = existing; return; }
        new GameObject("WireConnectionMinigame").AddComponent<WireConnectionMinigame>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        BuildInterface();
        SetOpen(showOnStart);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[openKey].wasPressedThisFrame)
            SetOpen(!isOpen);

        if (activeDrag >= 0 && previewLine != null && Mouse.current != null)
            UpdateLine(previewLine, GetEndpointPoint(activeDrag, true), GetBoardPoint(Mouse.current.position.ReadValue()));
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventObject.GetComponent<EventSystem>();
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null &&
                 eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Wire Task Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlay = CreateUIObject("Dim Background", canvasObject.transform);
        Stretch(overlay.GetComponent<RectTransform>());
        Image dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.015f, 0.025f, 0.04f, 0.88f);

        GameObject panel = CreateUIObject("Electrical Panel", overlay.transform);
        board = panel.GetComponent<RectTransform>();
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
        board.pivot = new Vector2(0.5f, 0.5f);
        board.sizeDelta = new Vector2(1050, 690);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.075f, 0.105f, 0.14f, 1f);

        CreateText("HEADER", board, "NAPRAWA OKABLOWANIA", 30, FontStyles.Bold,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -48), new Vector2(700, 55));

        CreateText("SUBHEADER", board, "PRZECIĄGNIJ KAŻDY PRZEWÓD DO GNIAZDA W TYM SAMYM KOLORZE", 15,
            FontStyles.Normal, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -91), new Vector2(900, 34));

        CreateText("LEFT LABEL", board, "WEJŚCIA", 14, FontStyles.Bold,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(145, -145), new Vector2(160, 30));
        CreateText("RIGHT LABEL", board, "WYJŚCIA", 14, FontStyles.Bold,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-145, -145), new Vector2(160, 30));

        statusText = CreateText("STATUS", board, "OCZEKIWANIE NA POŁĄCZENIE...", 16, FontStyles.Bold,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 82), new Vector2(700, 35));
        progressText = CreateText("PROGRESS", board, "POŁĄCZONO: 0 / 5", 14, FontStyles.Normal,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(400, 28));

        BuildEndpoints();

        Button close = CreateButton("ZAMKNIJ  [F3]", board, new Vector2(0.5f, 0), new Vector2(0, 1), new Vector2(0, 18), new Vector2(190, 42));
        close.onClick.AddListener(() => SetOpen(false));
    }

    private void BuildEndpoints()
    {
        int[] shuffled = { 0, 1, 2, 3, 4 };
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        for (int i = 0; i < 5; i++)
        {
            float y = -190f - i * 82f;
            CreateEndpoint("INPUT " + i, i, true, WireColors[i],
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(155, y));

            int colorIndex = shuffled[i];
            RectTransform target = CreateEndpoint("OUTPUT " + i, colorIndex, false, WireColors[colorIndex],
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-155, y));
            targetRects.Add(target);
            targetColors.Add(colorIndex);
        }
    }

    private RectTransform CreateEndpoint(string objectName, int colorIndex, bool source, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position)
    {
        GameObject endpoint = CreateUIObject(objectName, board);
        RectTransform rect = endpoint.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(source ? 0f : 1f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(76, 48);

        Image image = endpoint.AddComponent<Image>();
        image.color = new Color(0.025f, 0.04f, 0.055f, 1f);

        GameObject core = CreateUIObject("Colored Contact", endpoint.transform);
        RectTransform coreRect = core.GetComponent<RectTransform>();
        coreRect.anchorMin = coreRect.anchorMax = new Vector2(0.5f, 0.5f);
        coreRect.sizeDelta = new Vector2(24, 24);
        Image coreImage = core.AddComponent<Image>();\n        coreImage.color = color;\n        coreImage.raycastTarget = false;

        if (source)
        {
            WireDragHandle handle = endpoint.AddComponent<WireDragHandle>();
            handle.Initialize(this, colorIndex);
        }
        return rect;
    }

    public void BeginWireDrag(int colorIndex, Vector2 pointerPosition)
    {
        if (!isOpen || connected[colorIndex]) return;
        activeDrag = colorIndex;
        previewLine = CreateLine("Wire Preview", WireColors[colorIndex], 9);
        UpdateLine(previewLine, GetBoardPoint(pointerPosition), GetEndpointPoint(colorIndex, true));
        statusText.text = "PRZECIĄGNIJ PRZEWÓD DO PASUJĄCEGO KOLORU";
        statusText.color = Color.white;
    }

    public void MoveWireDrag(Vector2 pointerPosition)
    {
        if (activeDrag < 0 || previewLine == null) return;
        UpdateLine(previewLine, GetEndpointPoint(activeDrag, true), GetBoardPoint(pointerPosition));
    }

    public void FinishWireDrag(Vector2 pointerPosition)
    {
        if (activeDrag < 0) return;
        int color = activeDrag;
        int matchedTarget = -1;
        for (int i = 0; i < targetRects.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(targetRects[i], pointerPosition, null))
            {
                matchedTarget = i;
                break;
            }
        }

        if (previewLine != null) Destroy(previewLine.gameObject);
        previewLine = null;
        activeDrag = -1;

        if (matchedTarget >= 0 && targetColors[matchedTarget] == color && !connected[color])
        {
            connected[color] = true;
            RectTransform line = CreateLine("Connected Wire " + color, WireColors[color], 9);
            UpdateLine(line, GetEndpointPoint(color, true), GetTargetPoint(matchedTarget));
            wireRects.Add(line);
            statusText.text = "DOBRZE! PRZEWÓD PODŁĄCZONY.";
            statusText.color = new Color(0.35f, 1f, 0.58f);
            UpdateProgress();
        }
        else
        {
            statusText.text = matchedTarget < 0 ? "NIE PODŁĄCZONO. SPRÓBUJ PONOWNIE." : "ZŁY KOLOR! SPRÓBUJ PONOWNIE.";
            statusText.color = new Color(1f, 0.42f, 0.38f);
        }
    }

    private void UpdateProgress()
    {
        int count = 0;
        foreach (bool value in connected) if (value) count++;
        progressText.text = "POŁĄCZONO: " + count + " / 5";
        if (count == 5)
        {
            statusText.text = "ZASILANIE PRZYWRÓCONE! ZADANIE WYKONANE.";
            statusText.color = new Color(0.35f, 1f, 0.58f);
        }
    }

    private Vector2 GetEndpointPoint(int colorIndex, bool source)
    {
        RectTransform rect = source
            ? board.Find("INPUT " + colorIndex).GetComponent<RectTransform>()
            : targetRects[targetColors.IndexOf(colorIndex)];
        Vector3 world = rect.TransformPoint(new Vector3(source ? 76f : 0f, 24f, 0f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, RectTransformUtility.WorldToScreenPoint(null, world), null, out Vector2 local);
        return local;
    }

    private Vector2 GetTargetPoint(int targetIndex)
    {
        RectTransform rect = targetRects[targetIndex];
        Vector3 world = rect.TransformPoint(new Vector3(-76f, 0f, 0f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, RectTransformUtility.WorldToScreenPoint(null, world), null, out Vector2 local);
        return local;
    }

    private Vector2 GetBoardPoint(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPosition, null, out Vector2 local);
        return local;
    }

    private RectTransform CreateLine(string lineName, Color color, float thickness)
    {
        GameObject line = CreateUIObject(lineName, board);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(10, thickness);
        Image lineImage = line.AddComponent<Image>();\n        lineImage.color = color;\n        lineImage.raycastTarget = false;
        return rect;
    }

    private void UpdateLine(RectTransform line, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        line.anchoredPosition = start;
        line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
        line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void SetOpen(bool value)
    {
        isOpen = value;
        if (overlay != null) overlay.SetActive(value);
        if (!value && previewLine != null) Destroy(previewLine.gameObject);
        activeDrag = -1;
        if (canvas != null) canvas.gameObject.SetActive(true);
    }

    private void ResetTask()
    {
        for (int i = 0; i < connected.Length; i++) connected[i] = false;
        foreach (RectTransform line in wireRects) if (line != null) Destroy(line.gameObject);
        wireRects.Clear();
        UpdateProgress();
        statusText.text = "PODŁĄCZ PRZEWODY W ODPOWIEDNICH KOLORACH.";
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, string value, int size, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
    {
        GameObject go = CreateUIObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = new Color(0.88f, 0.93f, 0.97f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        return text;
    }

    private static Button CreateButton(string label, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 dimensions)
    {
        GameObject go = CreateUIObject("Button - " + label, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.25f, 0.34f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", rect, label, 14, FontStyles.Bold,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(text.rectTransform);
        return button;
    }

}

/// <summary>Pointer input adapter for a single wire connector.</summary>
public sealed class WireDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private WireConnectionMinigame owner;
    private int colorIndex;

    public void Initialize(WireConnectionMinigame minigame, int index)
    {
        owner = minigame;
        colorIndex = index;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null) owner.BeginWireDrag(colorIndex, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.MoveWireDrag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null) owner.FinishWireDrag(eventData.position);
    }
}
