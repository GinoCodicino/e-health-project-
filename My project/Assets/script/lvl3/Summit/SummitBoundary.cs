using UnityEngine;

/// <summary>
/// Invisible ring of walls around the summit, so the player can't walk down the mountain.
/// The walls are built at startup around this object (radius, height, number of segments).
/// </summary>
public class SummitBoundary : MonoBehaviour
{
    [SerializeField] private float radius = 4.2f;
    [SerializeField] private float height = 6f;
    [Tooltip("How far the walls go below this object (for slopes).")]
    [SerializeField] private float depth = 2f;
    [SerializeField] private float thickness = 0.4f;
    [SerializeField] private int segments = 20;

    private void Awake()
    {
        float step = 360f / segments;
        // Chord length + overlap so there are no gaps between the segments.
        float width = 2f * radius * Mathf.Tan(Mathf.Deg2Rad * step * 0.5f) + thickness;
        for (int i = 0; i < segments; i++)
        {
            var wall = new GameObject("Wall_" + i);
            wall.layer = 2; // Ignore Raycast: doesn't disturb ground raycasts
            wall.transform.SetParent(transform, false);
            Quaternion rot = Quaternion.Euler(0f, i * step, 0f);
            wall.transform.localRotation = rot;
            wall.transform.localPosition = rot * Vector3.forward * (radius + thickness * 0.5f)
                                           + Vector3.up * (height * 0.5f - depth);
            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, thickness);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Vector3 prev = transform.position + transform.forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = transform.position + Quaternion.Euler(0f, i * 360f / segments, 0f) * transform.forward * radius;
            Gizmos.DrawLine(prev, next);
            Gizmos.DrawLine(next, next + Vector3.up * (height - depth));
            prev = next;
        }
    }
}
