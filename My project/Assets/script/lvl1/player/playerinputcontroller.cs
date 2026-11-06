using UnityEngine;
using UnityEngine.InputSystem;

public class playerinputcontroller : MonoBehaviour
{
    public Vector2 movementInputVector { get; private set; }
    public Vector2 lookInputVector { get; private set; }
    public bool sprintHeld { get; private set; }

    private bool jumpRequested;
    private bool interactRequested;

    private void Awake()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.currentActionMap == null)
            playerInput.SwitchCurrentActionMap("Player");
    }

    private void OnMove(InputValue inputValue)
    {
        movementInputVector = inputValue.Get<Vector2>();
    }

    private void OnLook(InputValue inputValue)
    {
        lookInputVector = inputValue.Get<Vector2>();
    }

    private void OnSprint(InputValue inputValue)
    {
        sprintHeld = inputValue.isPressed;
    }

    private void OnJump(InputValue inputValue)
    {
        if (inputValue.isPressed)
            jumpRequested = true;
    }

    private void OnInteract(InputValue inputValue)
    {
        if (inputValue.isPressed)
            interactRequested = true;
    }

    public bool ConsumeJumpRequest()
    {
        bool requested = jumpRequested;
        jumpRequested = false;
        return requested;
    }

    public bool ConsumeInteractRequest()
    {
        bool requested = interactRequested;
        interactRequested = false;
        return requested;
    }

    private void OnDisable()
    {
        movementInputVector = Vector2.zero;
        lookInputVector = Vector2.zero;
        sprintHeld = false;
        jumpRequested = false;
        interactRequested = false;
    }
}
