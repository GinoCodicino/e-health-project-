using UnityEngine;

/// <summary>Teleports the player (return from the mini-game, facing an NPC...).</summary>
public static class PlayerPlacement
{
    /// <summary>
    /// Puts the player on the ground below position, turned to yaw. The ground is not at y = 0:
    /// we find it with a downward raycast (ignoring the player).
    /// The CharacterController is disabled during the move, otherwise it overwrites the position.
    /// </summary>
    public static void PlaceOnGround(Transform player, Vector3 position, float yaw)
    {
        Vector3 origin = position + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 target = position;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player)) continue;
            target = hit.point + Vector3.up * 0.05f;
            break;
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = true;
    }
}
