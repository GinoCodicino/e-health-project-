using UnityEngine;

/// <summary>
/// The scene's wind: a direction, a base strength and gusts (smooth noise).
/// Drives the "Polygon Wind/Tree URP" vegetation (global _PolygonWind), WindAudio and WindParticles.
/// One per scene.
/// </summary>
public class WindSystem : MonoBehaviour
{
    private static readonly int PolygonWindId = Shader.PropertyToID("_PolygonWind");

    [Tooltip("Where the wind blows to, in degrees around the vertical axis (0 = +Z, 90 = +X).")]
    [SerializeField] private float directionYaw = 60f;
    [Tooltip("Slight vertical motion of the vegetation (the original shader's default is 0.05).")]
    [SerializeField] private float verticalSway = 0.05f;

    [Header("Strength")]
    [Tooltip("Strength between gusts (1 = the vegetation moves as tuned in its material).")]
    [SerializeField] private float baseStrength = 0.8f;
    [Tooltip("Extra strength at the peak of a gust.")]
    [SerializeField] private float gustStrength = 1.2f;
    [Tooltip("How often gusts come (higher = more frequent).")]
    [SerializeField] private float gustFrequency = 0.12f;
    [Tooltip("Higher = gusts are rarer and sharper, with calmer moments in between.")]
    [Range(1f, 4f)] [SerializeField] private float gustSharpness = 2f;

    public static WindSystem Instance { get; private set; }

    /// <summary>Horizontal direction the wind blows to (normalized).</summary>
    public Vector3 Direction { get; private set; }
    /// <summary>0 = calm, 1 = peak of a gust.</summary>
    public float Gust01 { get; private set; }
    /// <summary>Current strength: baseStrength + gusts.</summary>
    public float Strength => baseStrength + gustStrength * Gust01;

    private float seed;

    private void OnEnable()
    {
        Instance = this;
        seed = Random.value * 100f;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        Shader.SetGlobalVector(PolygonWindId, Vector4.zero);
    }

    private void Update()
    {
        float yaw = directionYaw * Mathf.Deg2Rad;
        Direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

        // Two octaves of noise: slow swells plus a bit of turbulence.
        float t = Time.time * gustFrequency;
        float n = Mathf.PerlinNoise(seed, t) * 0.75f + Mathf.PerlinNoise(seed + 37f, t * 3.1f) * 0.25f;
        Gust01 = Mathf.Pow(Mathf.Clamp01((n - 0.25f) / 0.6f), gustSharpness);

        // The shader reads x and z as amplitudes per axis (as in the original _wind_dir).
        Vector3 dir = Direction * 0.7f + Vector3.up * verticalSway;
        Shader.SetGlobalVector(PolygonWindId, new Vector4(dir.x, dir.y, dir.z, Strength));
    }
}
