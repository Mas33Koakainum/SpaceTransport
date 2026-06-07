using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NetworkMouseLook : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float sensitivity = 1f;
    [SerializeField] private float sendYawThreshold = 1f;
    [SerializeField] private float remoteLerpSpeed = 10f;

    [Header("Pause / Menu")]
    [SerializeField] private GameObject pauseMenuCanvas;
    // pauseGameOnMenu usunięte – menu opcji nie zatrzymuje gry

    private PlayerInput input;
    private Vector2 lookInput;
    private float xRotation = 0f;

    private NetworkVariable<float> yawRotation = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private float lastSentYaw = 0f;
    private bool inputActive = false;
    private bool cameraEnabled = true;

    void Awake()
    {
        input = new PlayerInput();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Zawsze ukryj menu na starcie – dla każdego gracza niezależnie
        if (pauseMenuCanvas != null)
            pauseMenuCanvas.SetActive(false);

        if (IsOwner)
        {
            input.Player.Look.performed += OnLookPerformed;
            input.Player.Look.canceled += OnLookCanceled;
            input.Enable();
            inputActive = true;

            SetCursorLocked(true);

            // ── KLUCZOWA POPRAWKA ──────────────────────────────────────────────
            // Canvas w trybie Screen Space - Overlay renderuje się u WSZYSTKICH.
            // Przestawiamy go na Screen Space - Camera i podpinamy kamerę
            // lokalnego gracza – dzięki temu widzi go tylko właściciel prefaba.
            if (pauseMenuCanvas != null && cameraHolder != null)
            {
                var canvas = pauseMenuCanvas.GetComponent<Canvas>();
                var cam = cameraHolder.GetComponentInChildren<Camera>();
                if (canvas != null && cam != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1f;
                }
            }
            // ──────────────────────────────────────────────────────────────────

            lastSentYaw = transform.eulerAngles.y;
            yawRotation.OnValueChanged += OnYawVariableChanged;
            cameraEnabled = true;
        }
        else
        {
            // Wyłącz kamerę i AudioListener u nie-właścicieli
            if (cameraHolder != null)
            {
                if (cameraHolder.TryGetComponent<Camera>(out var cam))
                    cam.enabled = false;

                var camChild = cameraHolder.GetComponentInChildren<Camera>();
                if (camChild != null) camChild.enabled = false;

                if (cameraHolder.TryGetComponent<AudioListener>(out var al))
                    al.enabled = false;

                var alChild = cameraHolder.GetComponentInChildren<AudioListener>();
                if (alChild != null) alChild.enabled = false;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        CleanupInputAndCursor();
    }

    void OnDisable()
    {
        CleanupInputAndCursor();
    }

    private void CleanupInputAndCursor()
    {
        if (input != null && inputActive)
        {
            input.Player.Look.performed -= OnLookPerformed;
            input.Player.Look.canceled -= OnLookCanceled;
            input.Disable();
            inputActive = false;
        }

        if (IsOwner)
            SetCursorLocked(false);

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.SetActive(false);

        yawRotation.OnValueChanged -= OnYawVariableChanged;
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnLookPerformed(InputAction.CallbackContext ctx) => lookInput = ctx.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext ctx) => lookInput = Vector2.zero;

    void Update()
    {
        if (!IsOwner)
        {
            float smoothedYaw = Mathf.LerpAngle(
                transform.eulerAngles.y, yawRotation.Value, Time.deltaTime * remoteLerpSpeed);
            transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleCameraEnabledAndMenu();

        if (!cameraEnabled)
        {
            lookInput = Vector2.zero;
            return;
        }

        // Pitch
        float mouseY = lookInput.y * sensitivity;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Yaw
        float mouseX = lookInput.x * sensitivity;
        transform.Rotate(Vector3.up * mouseX);

        float currentYaw = transform.eulerAngles.y;
        if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, lastSentYaw)) >= sendYawThreshold)
        {
            SubmitYawServerRpc(currentYaw);
            lastSentYaw = currentYaw;
        }
    }

    private void ToggleCameraEnabledAndMenu()
    {
        cameraEnabled = !cameraEnabled;
        SetCursorLocked(cameraEnabled);

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.SetActive(!cameraEnabled);

        if (cameraEnabled)
            lastSentYaw = transform.eulerAngles.y;
        else
            lookInput = Vector2.zero;
    }

    public void ShowPauseMenu()
    {
        if (!IsOwner) return;
        cameraEnabled = false;
        SetCursorLocked(false);
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        lookInput = Vector2.zero;
    }

    public void HidePauseMenu()
    {
        if (!IsOwner) return;
        cameraEnabled = true;
        SetCursorLocked(true);
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        lastSentYaw = transform.eulerAngles.y;
    }

    public void TogglePauseMenu()
    {
        if (!IsOwner) return;
        ToggleCameraEnabledAndMenu();
    }

    [ServerRpc]
    private void SubmitYawServerRpc(float yaw)
    {
        yawRotation.Value = yaw;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void OnYawVariableChanged(float previous, float current) { }
}