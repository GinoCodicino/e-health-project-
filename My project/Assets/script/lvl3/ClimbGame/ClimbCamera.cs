using UnityEngine;

/// <summary>
/// Mini-game camera: with slight perspective and a slight low angle, it follows the climber
/// while looking a bit above him (to see hazards coming).
/// It sets the gradient sky (skybox) and the fog from the current section and the altitude.
/// Also provides the visible area on the gameplay plane (z = 0), used by the hazards.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ClimbCamera : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [SerializeField] private ClimbWeather weather;

    [Tooltip("The camera looks above the climber to see hazards coming (m).")]
    [SerializeField] private float lead = 3f;
    [SerializeField] private float smoothing = 5f;
    [SerializeField] private float skySmoothing = 2f;

    [Header("Sky")]
    [Tooltip("Sky material (Skybox/ClimbGradient shader). Empty = Resources/ClimbSky.")]
    [SerializeField] private Material skyMaterial;
    [Tooltip("The higher you climb, the darker the zenith (0 = no effect, 1 = black at the summit).")]
    [Range(0f, 1f)] [SerializeField] private float altitudeDarkening = 0.4f;

    [Header("Framing")]
    [Tooltip("Distance between the camera and the wall (m).")]
    [SerializeField] private float distance = 15f;
    [Tooltip("Vertical field of view. Small = almost flat, large = more depth.")]
    [SerializeField] private float fieldOfView = 40f;
    [Tooltip("Low angle: the camera is lower than what it looks at (degrees).")]
    [SerializeField] private float tilt = 8f;

    /// <summary>The camera never goes higher than this (the summit).</summary>
    public float MaxY { get; set; } = float.MaxValue;

    // Read by ClimbGameManager.Awake, which may run before ours: fetched on demand.
    private Camera cam;
    private Material sky;           // copy of the material: the asset is not modified
    private Color horizonColor, topColor;
    public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

    /// <summary>Altitude being looked at (screen center on the wall).</summary>
    public float FocusY { get; private set; }

    /// <summary>Visible half-height on the gameplay plane (m).</summary>
    public float HalfHeight => Cam.orthographic ? Cam.orthographicSize
                                                : distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    public float Top => FocusY + HalfHeight;
    public float Bottom => FocusY - HalfHeight;

    private void Awake()
    {
        Cam.orthographic = false;
        Cam.fieldOfView = fieldOfView;
        SetupSky();
    }

    private void OnDestroy()
    {
        if (sky != null) Destroy(sky);
    }

    // Gradient sky if the shader is available, otherwise a solid color as before.
    private void SetupSky()
    {
        Material source = skyMaterial != null ? skyMaterial : Resources.Load<Material>("ClimbSky");
        if (source != null && source.shader.isSupported)
        {
            sky = new Material(source);
            RenderSettings.skybox = sky;
            Cam.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            Cam.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    /// <summary>Snaps the camera onto the climber, without smoothing (start, back to checkpoint).</summary>
    public void Snap()
    {
        FocusY = TargetY();
        Place();
        if (weather.Current != null)
        {
            horizonColor = weather.Current.sky;
            topColor = weather.Current.SkyTop;
            ApplySky();
        }
    }

    private void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        FocusY = Mathf.Lerp(FocusY, TargetY(), t);
        Place();

        if (weather.Current != null)
        {
            float s = skySmoothing * Time.deltaTime;
            horizonColor = Color.Lerp(horizonColor, weather.Current.sky, s);
            topColor = Color.Lerp(topColor, weather.Current.SkyTop, s);
            ApplySky();
        }
    }

    // The camera is below the target point and looks up at it.
    private void Place()
    {
        float below = distance * Mathf.Tan(tilt * Mathf.Deg2Rad);
        transform.SetPositionAndRotation(new Vector3(0f, FocusY - below, -distance), Quaternion.Euler(-tilt, 0f, 0f));
    }

    // The fog takes the horizon color: the background mountains blend into it.
    private void ApplySky()
    {
        Cam.backgroundColor = horizonColor;
        RenderSettings.fogColor = horizonColor;
        if (sky == null) return;

        float altitude = MaxY < float.MaxValue ? Mathf.Clamp01(FocusY / MaxY) : 0f;
        Color top = Color.Lerp(topColor, Color.black, altitude * altitudeDarkening);
        sky.SetColor(TopId, top);
        sky.SetColor(HorizonId, horizonColor);
        sky.SetColor(BottomId, horizonColor * 0.85f);
    }

    /// <summary>Current horizon color (to tint distant clouds).</summary>
    public Color HorizonColor => horizonColor;

    private static readonly int TopId = Shader.PropertyToID("_TopColor");
    private static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
    private static readonly int BottomId = Shader.PropertyToID("_BottomColor");

    private float TargetY() => Mathf.Min(climber.Height + lead, MaxY);
}
