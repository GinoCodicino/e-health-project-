using UnityEngine;

/// <summary>
/// Place le corps du joueur pendant l'escalade, face aux prises tenues.
/// Les yeux (la caméra) restent à wallDistance de la paroi, mesuré PERPENDICULAIREMENT
/// à la paroi : la distance ressentie est la même sur un passage raide ou couché.
/// Activé par PlayerModeController uniquement en mode Climbing.
/// </summary>
[RequireComponent(typeof(ClimbingHands))]
public class ClimbBody : MonoBehaviour
{
    [Tooltip("Distance entre les yeux et la paroi, perpendiculairement à la paroi (m).")]
    [SerializeField] private float wallDistance = 1.1f;
    [Tooltip("Les yeux sont un peu plus bas que les prises tenues (m).")]
    [SerializeField] private float eyeBelowHands = 0.4f;
    [SerializeField] private float bodySpeed = 4f;
    [Tooltip("Vitesse à laquelle on suit l'orientation de la paroi (évite les à-coups entre facettes).")]
    [SerializeField] private float normalSmoothing = 3f;
    [Tooltip("Calques considérés comme paroi.")]
    [SerializeField] private LayerMask wallMask = ~0;

    private ClimbingHands hands;
    private float eyeHeight = 1.5f;   // hauteur de la caméra au-dessus des pieds
    private Vector3 wallNormal;       // normale de la paroi (lissée), pointe vers le joueur
    private bool hasNormal;

    private void Awake()
    {
        hands = GetComponent<ClimbingHands>();

        Camera cam = Camera.main;
        if (cam != null && cam.transform.IsChildOf(transform))
            eyeHeight = cam.transform.position.y - transform.position.y;
    }

    private void OnEnable()
    {
        // Avant la première mesure : on suppose la paroi droit devant le joueur.
        Vector3 back = -transform.forward;
        back.y = 0f;
        wallNormal = back.normalized;
        hasNormal = false;
    }

    private void LateUpdate()
    {
        if (!hands.TryGetHoldsCenter(out Vector3 center)) return;

        // 1. Orientation de la paroi derrière les prises
        if (TryGetWallNormal(center, out Vector3 n))
        {
            if (!hasNormal) wallNormal = n;   // première mesure : on la prend telle quelle
            float s = 1f - Mathf.Exp(-normalSmoothing * Time.deltaTime);
            wallNormal = Vector3.Slerp(wallNormal, n, s).normalized;
            hasNormal = true;
        }

        // 2. Où doivent être les yeux ? Écartés de la paroi, un peu sous les mains.
        Vector3 eye = center + wallNormal * wallDistance - Vector3.up * eyeBelowHands;

        // 3. Le corps est sous les yeux. Lissage indépendant du framerate.
        Vector3 target = eye - Vector3.up * eyeHeight;
        float t = 1f - Mathf.Exp(-bodySpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }

    // Rayon tiré depuis l'extérieur de la paroi vers le centre des prises.
    // On ignore les prises et le joueur : le premier autre objet touché est la paroi.
    private bool TryGetWallNormal(Vector3 center, out Vector3 normal)
    {
        Vector3 origin = center + wallNormal * 3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, -wallNormal, 6f, wallMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponent<Hold>() != null) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            normal = hit.normal;
            return true;
        }
        normal = Vector3.zero;
        return false;
    }
}
