using UnityEngine;

/// <summary>
/// Point at the foot of the route, in the mountain scene.
/// When returning from the climbing mini-game (quit), the player is placed back here and the screen fades in.
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

    // Puts the player on the ground below this point. The ground is not at y = 0:
    // we find it with a downward raycast (ignoring the player).
    // The CharacterController is disabled during the move, otherwise it overwrites the position.
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
