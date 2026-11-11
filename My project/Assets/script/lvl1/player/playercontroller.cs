using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(playerinputcontroller))]
public class playercontroller : MonoBehaviour
{
    [Header("Modalità adattatore")]
    [Tooltip("Attivalo se il personaggio ha già il suo controller. Questo script non muoverà nulla, ma potrà bloccare i componenti indicati durante dialoghi e minigiochi.")]
    [SerializeField] private bool soloBlocco = false;

    [Tooltip("Componenti da disattivare quando canMove è false, ad esempio PlayerInput, script di interazione, arma, ecc.")]
    [SerializeField] private Behaviour[] componentiDaBloccare;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;

    [Header("Jump and gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float maxLookAngle = 80f;

    private playerinputcontroller inputController;
    private CharacterController characterController;

    private float cameraPitch;
    private float verticalVelocity;

    private bool puoMuoversi = true;

    public bool canMove
    {
        get => puoMuoversi;
        set
        {
            puoMuoversi = value;

            if (componentiDaBloccare == null)
                return;

            foreach (Behaviour componente in componentiDaBloccare)
            {
                if (componente != null)
                    componente.enabled = value;
            }
        }
    }

    private void Awake()
    {
        inputController = GetComponent<playerinputcontroller>();
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (pitchPivot == null && Camera.main != null)
            pitchPivot = Camera.main.transform;

        LockCursor(true);
    }

    private void Update()
    {
        if (soloBlocco)
            return;

        if (!canMove)
            return;

        HandleCursor();
        HandleLook();
        HandleMovement();
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(false);

        if (Input.GetMouseButtonDown(0) &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
        }
    }

    private void HandleMovement()
    {
        bool grounded = characterController.isGrounded;

        // Mantiene il contatto con il terreno quando non saltiamo.
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        Vector2 input = inputController.movementInputVector;

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        // Evita una velocità maggiore nelle diagonali,
        // conservando l'intensità di un eventuale stick analogico.
        move = Vector3.ClampMagnitude(move, 1f);

        float speed = inputController.sprintHeld
            ? sprintSpeed
            : moveSpeed;

        bool jumpPressed = inputController.ConsumeJumpRequest();

        if (jumpPressed && grounded)
        {
            verticalVelocity = Mathf.Sqrt(
                jumpHeight * -2f * gravity
            );
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            move * speed +
            Vector3.up * verticalVelocity;

        CollisionFlags collisions = characterController.Move(
            velocity * Time.deltaTime
        );

        // Se colpiamo un soffitto, interrompiamo la salita.
        if ((collisions & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    /// <summary>Sets the camera's vertical angle (0 = horizontal), e.g. to face an NPC.</summary>
    public void SetPitch(float pitch)
    {
        cameraPitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 look = inputController.lookInputVector;

        float mouseX = look.x * mouseSensitivity;
        float mouseY = look.y * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        if (pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(
                cameraPitch,
                0f,
                0f
            );
        }
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }
}
