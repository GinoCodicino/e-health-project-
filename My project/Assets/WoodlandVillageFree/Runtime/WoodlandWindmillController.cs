using UnityEngine;

namespace WoodlandVillageFree {
/// <summary>Rotates only a windmill's authored rotor. Speed is signed degrees per second.</summary>
[DisallowMultipleComponent]
public sealed class WoodlandWindmillController : MonoBehaviour {
    [SerializeField] Transform rotor;
    [SerializeField] Vector3 localAxis = Vector3.forward;
    [SerializeField] float degreesPerSecond = 18f;
    [SerializeField] bool autoStart = true;
    bool isRotating;

    public Transform Rotor { get => rotor; set => rotor = value; }
    public Vector3 LocalAxis { get => localAxis; set => localAxis = value.sqrMagnitude > 0.000001f ? value.normalized : Vector3.forward; }
    public float DegreesPerSecond { get => degreesPerSecond; set => degreesPerSecond = float.IsNaN(value) || float.IsInfinity(value) ? 0f : value; }
    public bool AutoStart { get => autoStart; set => autoStart = value; }
    public bool IsRotating => isRotating;

    void Start() { if (autoStart) StartRotating(); }
    void Update() { Step(Time.deltaTime); }
    void Step(float seconds) {
        if (!isRotating || rotor == null || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        rotor.Rotate(LocalAxis, DegreesPerSecond * seconds, Space.Self);
    }
    public void StartRotating() { isRotating = true; }
    public void StartRotating(float speed) { DegreesPerSecond = speed; StartRotating(); }
    public void StopRotating() { isRotating = false; }
    public void SetSpeed(float speed) { DegreesPerSecond = speed; }
    void OnValidate() { LocalAxis = localAxis; DegreesPerSecond = degreesPerSecond; }
}
}
