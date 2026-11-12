using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class NetworkPlayerController : NetworkBehaviour
{
    private PlayerInput input;
    private Rigidbody rb;

    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    private Vector2 moveInput;
    private bool jumpPressed;

    // Serwerowe bufory inputu — wykorzystywane w FixedUpdate (authoritative)
    private Vector2 serverMoveInput = Vector2.zero;
    private bool serverJumpRequested = false;

    [Header("References")]
    public Transform visual;        // obiekt Visual / model gracza + CameraHolder
    public Transform cameraHolder;  // kamera do pitch

    private void Awake()
    {
        input = new PlayerInput();
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            input.Enable();
            input.Player.Jump.performed += ctx => jumpPressed = true;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // While typing in chat, prevent keyboard input from moving or jumping the player.
        if (SpaceTransportChat.IsChatOpen)
        {
            moveInput = Vector2.zero;
            jumpPressed = false;
            SubmitInputServerRpc(Vector2.zero, false);
            return;
        }

        ReadMovement();

        // Wysyłamy input do serwera (tylko zapisuje stan po stronie serwera)
        SubmitInputServerRpc(moveInput, jumpPressed);

        // lokalnie resetujemy sygnał skoku - serwer obsłuży jego wykonanie
        jumpPressed = false;
    }

    private void ReadMovement()
    {
        moveInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            moveInput.y += 1;

        if (Keyboard.current.sKey.isPressed)
            moveInput.y -= 1;

        if (Keyboard.current.dKey.isPressed)
            moveInput.x += 1;

        if (Keyboard.current.aKey.isPressed)
            moveInput.x -= 1;

        moveInput.Normalize();
    }

    [ServerRpc]
    private void SubmitInputServerRpc(Vector2 move, bool jump, ServerRpcParams rpcParams = default)
    {
        // zapisujemy ostatni input od klienta; wykonywanie ruchu robimy w FixedUpdate (server)
        serverMoveInput = move;
        if (jump)
            serverJumpRequested = true;
    }

    // FixedUpdate uruchamiany zgodnie z pętlą fizyki — daje stabilny, niezależny od FPS ruch
    private void FixedUpdate()
    {
        if (!IsServer) return; // authoritative movement tylko na serwerze

        // jeśli brak referencji visual, nie próbujemy obracać kierunku
        if (serverMoveInput != Vector2.zero && visual != null)
        {
            Vector3 forward = visual.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = visual.right;
            right.y = 0f;
            right.Normalize();

            Vector3 moveDir = (forward * serverMoveInput.y + right * serverMoveInput.x).normalized;
            ApplyMovement(moveDir, Time.fixedDeltaTime);
        }

        if (serverJumpRequested)
        {
            Jump();
            serverJumpRequested = false;
        }
    }

    // Aplikujemy ruch używając fixedDeltaTime (stabilne dla fizyki)
    private void ApplyMovement(Vector3 moveDir, float deltaTime)
    {
        if (moveDir == Vector3.zero) return;
        rb.MovePosition(rb.position + moveDir * moveSpeed * deltaTime);
    }

    private void Jump()
    {
        // skok używa ForceMode.Impulse — jest poprawnie aplikowany w FixedUpdate (serwer)
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
}