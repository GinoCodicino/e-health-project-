using UnityEngine;

/// <summary>
/// Caméra du mini-jeu : en légère perspective et légère contre-plongée, elle suit le grimpeur
/// en regardant un peu au-dessus de lui (pour voir arriver les dangers).
/// Elle prend la couleur du ciel du tronçon courant (fond et brouillard lointain).
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
        Cam.clearFlags = CameraClearFlags.SolidColor;
    }

    /// <summary>Place la caméra directement sur le grimpeur, sans lissage (début, retour au point de contrôle).</summary>
    public void Snap()
    {
        FocusY = TargetY();
        Place();
        if (weather.Current != null) SetSky(weather.Current.sky);
    }

    private void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        FocusY = Mathf.Lerp(FocusY, TargetY(), t);
        Place();

        if (weather.Current != null)
            SetSky(Color.Lerp(Cam.backgroundColor, weather.Current.sky, skySmoothing * Time.deltaTime));
    }

    // La caméra est sous le point regardé et lève les yeux vers lui.
    private void Place()
    {
        float below = distance * Mathf.Tan(tilt * Mathf.Deg2Rad);
        transform.SetPositionAndRotation(new Vector3(0f, FocusY - below, -distance), Quaternion.Euler(-tilt, 0f, 0f));
    }

    // Le brouillard prend la couleur du ciel : les montagnes du fond se fondent dedans.
    private void SetSky(Color sky)
    {
        Cam.backgroundColor = sky;
        RenderSettings.fogColor = sky;
    }

    private float TargetY() => Mathf.Min(climber.Height + lead, MaxY);
}
