using UnityEngine;

/// <summary>
/// Spins a helicopter's rotors and slows them down, as if the engine was just turned off.
/// Put it on the helicopter and drag the main and tail rotor objects into the fields.
/// </summary>
public class HelicopterRotor : MonoBehaviour
{
    [Header("Rotors")]
    [Tooltip("Main (top) rotor. It turns around its local axis below.")]
    [SerializeField] private Transform mainRotor;
    [SerializeField] private Vector3 mainAxis = Vector3.up;

    [Tooltip("Tail rotor (optional).")]
    [SerializeField] private Transform tailRotor;
    [SerializeField] private Vector3 tailAxis = Vector3.right;
    [Tooltip("Tail rotor speed compared to the main rotor.")]
    [SerializeField] private float tailSpeedFactor = 2.5f;

    [Header("Slow down")]
    [Tooltip("Main rotor speed when the level starts (degrees per second).")]
    [SerializeField] private float startSpeed = 900f;
    [Tooltip("Speed reached at the end (0 = stopped).")]
    [SerializeField] private float endSpeed = 0f;
    [Tooltip("Time to go from the start speed to the end speed (seconds).")]
    [SerializeField] private float slowDownDuration = 25f;
    [Tooltip("Higher values keep the rotor fast longer, then slow down at the end.")]
    [SerializeField] private float easing = 2f;

    private float elapsed;

    /// <summary>Current main rotor speed in degrees per second.</summary>
    public float CurrentSpeed { get; private set; }

    private void OnEnable()
    {
        elapsed = 0f;
        CurrentSpeed = startSpeed;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = slowDownDuration > 0f ? Mathf.Clamp01(elapsed / slowDownDuration) : 1f;
        CurrentSpeed = Mathf.Lerp(startSpeed, endSpeed, Mathf.Pow(t, easing));

        if (CurrentSpeed <= 0.01f) return;

        float step = CurrentSpeed * Time.deltaTime;
        if (mainRotor != null) mainRotor.Rotate(mainAxis, step, Space.Self);
        if (tailRotor != null) tailRotor.Rotate(tailAxis, step * tailSpeedFactor, Space.Self);
    }
}
