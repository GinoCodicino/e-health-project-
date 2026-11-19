using UnityEngine;

/// <summary>
/// Makes a light breathe slowly, for a magical marker (the entrance of the climb).
/// </summary>
[RequireComponent(typeof(Light))]
public class GlowPulse : MonoBehaviour
{
    [Tooltip("Pulses per second.")]
    [SerializeField] private float speed = 0.6f;
    [Tooltip("How much the intensity changes (0 = steady, 1 = goes out).")]
    [Range(0f, 1f)] [SerializeField] private float amount = 0.35f;

    private Light glow;
    private float baseIntensity;

    private void Awake()
    {
        glow = GetComponent<Light>();
        baseIntensity = glow.intensity;
    }

    private void Update()
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * speed);
        glow.intensity = baseIntensity * (1f - amount * wave);
    }
}
