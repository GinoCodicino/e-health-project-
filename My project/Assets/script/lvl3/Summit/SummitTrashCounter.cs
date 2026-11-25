using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Counts the litter picked up on the summit and shows "Litter picked up: 1/2"
/// while the player is near. Raises onAllCollected once everything is picked up
/// (used to unlock the guestbook).
/// </summary>
public class SummitTrashCounter : MonoBehaviour
{
    [Tooltip("The counter is shown when the player is within this distance (m).")]
    [SerializeField] private float showRadius = 12f;
    [SerializeField] private int fontSize = 26;
    [SerializeField] private string label = "Litter picked up";
    [SerializeField] private string doneMessage = "The summit is clean.";
    [SerializeField] private float doneMessageDuration = 3f;

    [Tooltip("Called once all the litter is picked up.")]
    public UnityEvent onAllCollected;

    private static readonly List<TrashPickup> trash = new List<TrashPickup>();
    private static SummitTrashCounter instance;

    public static bool AllCollected { get; private set; }
    public int Collected { get; private set; }
    public int Total => trash.Count;

    private Transform player;
    private float doneUntil;
    private GUIStyle style;

    public static void Register(TrashPickup item)
    {
        if (!trash.Contains(item)) trash.Add(item);
    }

    public static void NotifyPickedUp(TrashPickup item)
    {
        if (instance != null) instance.HandlePickedUp();
    }

    private void Awake()
    {
        instance = this;
        AllCollected = false;
        trash.RemoveAll(t => t == null);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        trash.Clear();
    }

    private void Start()
    {
        var pc = FindAnyObjectByType<playercontroller>();
        if (pc != null) player = pc.transform;
    }

    private void HandlePickedUp()
    {
        Collected = 0;
        foreach (var t in trash) if (t != null && t.PickedUp) Collected++;
        if (AllCollected || Collected < Total) return;

        AllCollected = true;
        doneUntil = Time.time + doneMessageDuration;
        onAllCollected.Invoke();
    }

    private void OnGUI()
    {
        if (player == null || Total == 0) return;
        if ((player.position - transform.position).sqrMagnitude > showRadius * showRadius) return;

        string text;
        if (!AllCollected) text = $"{label}: {Collected}/{Total}";
        else if (Time.time < doneUntil) text = doneMessage;
        else return;

        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        GUI.Label(new Rect(0, Screen.height * 0.05f, Screen.width, 60), text, style);
    }
}
