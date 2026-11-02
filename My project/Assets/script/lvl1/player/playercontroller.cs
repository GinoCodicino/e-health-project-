using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(playerinputcontroller))]
public class playercontroller : MonoBehaviour
{
    private playerinputcontroller inputController;
    private CharacterController characterController;

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

    private float cameraPitch;
    private float verticalVelocity;

    private void Awake()
    {
        inputController = GetComponent<playerinputcontroller>();
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
    {
        bool grounded = characterController.isGrounded;

        // Mantiene il contatto con il terreno quando non saltiamo.
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        Vector2 input = inputController.movementinputvector;

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

    private void HandleLook()
    {
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
                cameraPitch, 0f, 0f
            );
        }
    }
}
