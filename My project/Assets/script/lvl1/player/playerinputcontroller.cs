using UnityEngine;
using UnityEngine.InputSystem;

public class playerinputcontroller : MonoBehaviour
{
    public Vector2 movementInputVector { get; private set; }
    public Vector2 lookInputVector { get; private set; }
    public bool sprintHeld { get; private set; }

    private bool jumpRequested;

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

    public bool ConsumeJumpRequest()
    {
        bool requested = jumpRequested;
        jumpRequested = false;
        return requested;
    }

    private void OnDisable()
    {
        movementInputVector = Vector2.zero;
        lookInputVector = Vector2.zero;
        sprintHeld = false;
        jumpRequested = false;
    }
}
