using UnityEngine;

/// <summary>
/// Caméra du mini-jeu : suit le grimpeur en regardant un peu au-dessus de lui
/// (pour voir arriver les dangers) et prend la couleur du ciel du tronçon courant.
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

    /// <summary>La caméra ne monte pas plus haut (le sommet).</summary>
    public float MaxY { get; set; } = float.MaxValue;

    // Lu par ClimbGameManager.Awake, qui peut passer avant le nôtre : on le récupère à la demande.
    private Camera cam;
    public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();

    /// <summary>Place la caméra directement sur le grimpeur, sans lissage (début, retour au point de contrôle).</summary>
    public void Snap()
    {
        Vector3 p = transform.position;
        p.y = TargetY();
        transform.position = p;
        if (weather.Current != null) Cam.backgroundColor = weather.Current.sky;
    }

    private void LateUpdate()
    {
        Vector3 p = transform.position;
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        p.y = Mathf.Lerp(p.y, TargetY(), t);
        transform.position = p;

        if (weather.Current != null)
            Cam.backgroundColor = Color.Lerp(Cam.backgroundColor, weather.Current.sky, skySmoothing * Time.deltaTime);
    }

    private float TargetY() => Mathf.Min(climber.Height + lead, MaxY);
}
