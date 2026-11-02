using UnityEngine;

/// <summary>
/// Point au pied de la voie, dans la scène de la montagne.
/// Au retour du mini-jeu d'escalade (abandon), on y replace le joueur et on fait un fondu d'entrée.
/// </summary>
public class MountainReturnPoint : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.5f;

    private void Start()
    {
        if (!ClimbGameSession.ConsumeReturn()) return;

        var player = FindFirstObjectByType<playercontroller>();
        if (player != null) PlacePlayer(player.transform);

        var fader = FindFirstObjectByType<ScreenFader>();
        if (fader != null)
        {
            fader.SetAlpha(1f);
            StartCoroutine(fader.FadeTo(0f, fadeDuration));
        }
    }

    // Pose le joueur sur le sol sous ce point. Le sol n'est pas à y = 0 :
    // on le cherche avec un rayon vers le bas (en ignorant le joueur).
    // Le CharacterController est coupé le temps du déplacement, sinon il écrase la position.
    private void PlacePlayer(Transform player)
    {
        Vector3 origin = transform.position + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 target = transform.position;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player)) continue;
            target = hit.point + Vector3.up * 0.05f;
            break;
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(target, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
    }
}
