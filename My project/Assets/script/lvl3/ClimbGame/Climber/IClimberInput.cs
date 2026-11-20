using UnityEngine.InputSystem;

/// <summary>
/// Where lane changes come from. To use another device (gamepad, motion sensor...),
/// put a MonoBehaviour implementing this interface on the climber: LaneClimber picks it up.
/// </summary>
public interface IClimberInput
{
    /// <summary>-1 = one lane left, +1 = one lane right, 0 = nothing this frame.</summary>
    int ReadLaneChange();
}

/// <summary>Default input: ←/→ or A/D.</summary>
public class KeyboardClimberInput : IClimberInput
{
    public int ReadLaneChange()
    {
        var kb = Keyboard.current;
        if (kb == null) return 0;
        int direction = 0;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) direction--;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) direction++;
        return direction;
    }
}
