using UnityEngine;
using TMPro;

/// <summary>
/// Drop-in quest marker: a glowing ground ring, a vertical beam of light and a
/// floating label with the distance to the target.
/// Everything is generated at runtime (no prefabs, no colliders, no assets to import).
///
/// Usage:
///   1. Add this component to any GameObject (e.g. your minigame manager).
///   2. Assign "target" (the point to reach) in the Inspector, or call SetTarget() from code.
///   3. Make sure the player has the "Player" tag, or assign "player" manually.
/// </summary>
public class QuestMarker : MonoBehaviour
{
    [Header("Who and where")]
    public Transform target;          // point the marker is placed on
    public Transform player;          // if empty, the object tagged "Player" is used

    [Header("Look")]
    public float radius = 3f;         // radius of the ring = distance at which the player is "inside"
    public float height = 6f;         // height of the beam of light
    public Color color = new Color(1f, .7f, .1f);
    public float lineWidth = .08f;
    public int ringSegments = 48;
    public float ringHeight = .10f;   // lift the ring slightly above the ground

    [Header("Label")]
    public bool showLabel = true;
    public string title = "OBJECTIVE";
    public string insideText = "You are here";
    public float fontSize = 3f;

    /// <summary>True when the player is within "radius" of the target.</summary>
    public bool PlayerInside { get; private set; }
    /// <summary>Current distance (metres) between player and target.</summary>
    public float Distance { get; private set; }

    GameObject root;
    LineRenderer ring, beam;
    TextMeshPro label;
    Material material;

    void Start()
    {
        FindPlayer();
    }

    void FindPlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    /// <summary>Move the marker to a new target. Radius and title are optional.</summary>
    public void SetTarget(Transform newTarget, float newRadius = -1f, string newTitle = null)
    {
        target = newTarget;
        if (newRadius > 0f) radius = newRadius;
        if (newTitle != null) title = newTitle;
    }

    /// <summary>Hide the marker (e.g. objective completed).</summary>
    public void Hide()
    {
        target = null;
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if (target == null) { if (root != null) root.SetActive(false); return; }
        if (player == null) FindPlayer();
        if (root == null) Build();

        root.SetActive(true);
        root.transform.position = target.position;

        Distance = player != null ? Vector3.Distance(player.position, target.position) : 0f;
        PlayerInside = player != null && Distance <= radius;

        // Ring
        for (int i = 0; i < ringSegments; i++)
        {
            float a = i * Mathf.PI * 2f / ringSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, ringHeight, Mathf.Sin(a) * radius));
        }

        // Beam
        beam.SetPosition(0, Vector3.up * .15f);
        beam.SetPosition(1, Vector3.up * height);

        // Colour
        ring.startColor = ring.endColor = color;
        beam.startColor = beam.endColor = color;

        // Label
        label.gameObject.SetActive(showLabel);
        if (showLabel)
        {
            label.color = color;
            label.fontSize = fontSize;
            label.transform.localPosition = Vector3.up * (height + .35f);
            label.text = PlayerInside
                ? title + "\n" + insideText
                : title + "\n" + Mathf.CeilToInt(Distance) + " m";
        }
    }

    void LateUpdate()
    {
        // Make the label always face the camera.
        if (root == null || !root.activeSelf || label == null) return;
        Camera cam = Camera.main;
        if (cam != null) label.transform.rotation = cam.transform.rotation;
    }

    void Build()
    {
        root = new GameObject("Quest_Marker_Runtime");

        // Unlit material so the colour stays bright. Falls back to Sprites/Default if URP is missing.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) { material = new Material(shader); material.color = color; }

        ring = MakeLine("GroundRing", ringSegments, true);
        beam = MakeLine("Beacon", 2, false);

        GameObject text = new GameObject("MarkerLabel");
        text.transform.SetParent(root.transform, false);
        label = text.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(8f, 3f);
    }

    LineRenderer MakeLine(string lineName, int points, bool loop)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(root.transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;          // positions are relative to the marker
        line.positionCount = points;
        line.loop = loop;
        line.widthMultiplier = lineWidth;
        if (material != null) line.sharedMaterial = material;
        return line;
    }

    void OnDisable()
    {
        if (root != null) root.SetActive(false);
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
        if (material != null) Destroy(material);
    }

    // Preview the ring in the editor when the object is selected.
    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = color;
        Vector3 c = target.position;
        for (int i = 0; i < 48; i++)
        {
            float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), .1f, Mathf.Sin(a0)) * radius,
                            c + new Vector3(Mathf.Cos(a1), .1f, Mathf.Sin(a1)) * radius);
        }
    }
}
