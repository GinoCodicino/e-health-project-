using UnityEngine;

/// <summary>
/// Caméra du mini-jeu : en légère perspective et légère contre-plongée, elle suit le grimpeur
/// en regardant un peu au-dessus de lui (pour voir arriver les dangers).
/// Elle règle le ciel en dégradé (skybox) et le brouillard selon le tronçon courant et l'altitude.
/// Donne aussi la zone visible sur le plan de jeu (z = 0), utilisée par les dangers.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ClimbCamera : MonoBehaviour
{
    [SerializeField] private LaneClimber climber;
    [SerializeField] private ClimbWeather weather;

    [Tooltip("La caméra regarde au-dessus du grimpeur pour voir arriver les dangers (m).")]
    [SerializeField] private float lead = 3f;
    [SerializeField] private float smoothing = 5f;
    [SerializeField] private float skySmoothing = 2f;

    [Header("Ciel")]
    [Tooltip("Matériau du ciel (shader Skybox/ClimbGradient). Vide = Resources/ClimbSky.")]
    [SerializeField] private Material skyMaterial;
    [Tooltip("Plus on monte, plus le zénith fonce (0 = pas d'effet, 1 = noir au sommet).")]
    [Range(0f, 1f)] [SerializeField] private float altitudeDarkening = 0.4f;

    [Header("Cadrage")]
    [Tooltip("Distance entre la caméra et la paroi (m).")]
    [SerializeField] private float distance = 15f;
    [Tooltip("Angle de vue vertical. Petit = presque à plat, grand = plus de profondeur.")]
    [SerializeField] private float fieldOfView = 40f;
    [Tooltip("Contre-plongée : la caméra est plus bas que ce qu'elle regarde (degrés).")]
    [SerializeField] private float tilt = 8f;

    /// <summary>La caméra ne monte pas plus haut (le sommet).</summary>
    public float MaxY { get; set; } = float.MaxValue;

    // Lu par ClimbGameManager.Awake, qui peut passer avant le nôtre : on le récupère à la demande.
    private Camera cam;
    private Material sky;           // copie du matériau : on ne modifie pas l'asset
    private Color horizonColor, topColor;
    public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

    /// <summary>Altitude regardée (centre de l'écran sur la paroi).</summary>
    public float FocusY { get; private set; }

    /// <summary>Demi-hauteur visible sur le plan de jeu (m).</summary>
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

    // Ciel en dégradé si le shader est disponible, sinon couleur unie comme avant.
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

    /// <summary>Place la caméra directement sur le grimpeur, sans lissage (début, retour au point de contrôle).</summary>
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

    // La caméra est sous le point regardé et lève les yeux vers lui.
    private void Place()
    {
        float below = distance * Mathf.Tan(tilt * Mathf.Deg2Rad);
        transform.SetPositionAndRotation(new Vector3(0f, FocusY - below, -distance), Quaternion.Euler(-tilt, 0f, 0f));
    }

    // Le brouillard prend la couleur de l'horizon : les montagnes du fond se fondent dedans.
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

    /// <summary>Couleur actuelle de l'horizon (pour teinter les nuages lointains).</summary>
    public Color HorizonColor => horizonColor;

    private static readonly int TopId = Shader.PropertyToID("_TopColor");
    private static readonly int HorizonId = Shader.PropertyToID("_HorizonColor");
    private static readonly int BottomId = Shader.PropertyToID("_BottomColor");

    private float TargetY() => Mathf.Min(climber.Height + lead, MaxY);
}
