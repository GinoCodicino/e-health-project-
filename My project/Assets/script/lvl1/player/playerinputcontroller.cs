using UnityEngine;
using UnityEngine.InputSystem;

// Mantiene i nomi usati dal PlayerInput esistente (Send Messages).
public class playerinputcontroller : MonoBehaviour
{
    public Vector2 movementinputvector { get; private set; }
    public Vector2 lookInputVector { get; private set; }
    public bool sprintHeld { get; private set; }

    private bool jumpRequested;
    private bool gameplayInputEnabled = true;
    private static int escapeConsumedFrame = -1;

    private void OnMove(InputValue value)
    {
        movementinputvector = gameplayInputEnabled ? value.Get<Vector2>() : Vector2.zero;
    }

    private void OnLook(InputValue value)
    {
        lookInputVector = gameplayInputEnabled ? value.Get<Vector2>() : Vector2.zero;
    }

    private void OnSprint(InputValue value)
    {
        sprintHeld = gameplayInputEnabled && value.isPressed;
    }

    private void OnJump(InputValue value)
    {
        if (gameplayInputEnabled && value.isPressed) jumpRequested = true;
    }

    public bool ConsumeJumpRequest()
    {
        bool requested = jumpRequested;
        jumpRequested = false;
        return requested;
    }

    public void SetGameplayInputEnabled(bool value)
    {
        if (gameplayInputEnabled == value) return;
        gameplayInputEnabled = value;
        ClearInput();
    }

    public void ClearInput()
    {
        movementinputvector = Vector2.zero;
        lookInputVector = Vector2.zero;
        sprintHeld = false;
        jumpRequested = false;
    }

    // Impedisce che lo stesso ESC chiuda due pannelli nello stesso frame.
    public static bool ConsumeEscape()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame
            || escapeConsumedFrame == Time.frameCount) return false;
        escapeConsumedFrame = Time.frameCount;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() { escapeConsumedFrame = -1; }

    private void OnDisable() { ClearInput(); }
}
