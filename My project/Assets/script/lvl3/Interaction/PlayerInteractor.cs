using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On the player: finds the closest interactable in range, shows its prompt and calls it on E.
/// Interactables register themselves (no collider or physics layer needed).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("Max distance to an interactable, measured on the ground (m).")]
    [SerializeField] private float range = 2.5f;
    [SerializeField] private int fontSize = 28;

    private static readonly List<IInteractable> interactables = new List<IInteractable>();

    public static void Register(IInteractable interactable)
    {
        if (!interactables.Contains(interactable)) interactables.Add(interactable);
    }

    public static void Unregister(IInteractable interactable) => interactables.Remove(interactable);

    /// <summary>The interactable the prompt is shown for, null if none.</summary>
    public IInteractable Current { get; private set; }

    private GUIStyle style;

    private void Update()
    {
        Current = DialogueRunner.IsBusy ? null : FindClosest();

        var kb = Keyboard.current;
        if (Current != null && kb != null && kb.eKey.wasPressedThisFrame)
            Current.Interact(this);
    }

    private IInteractable FindClosest()
    {
        IInteractable closest = null;
        float best = range * range;
        Vector3 p = transform.position;
        foreach (var i in interactables)
        {
            if (!i.CanInteract) continue;
            Vector3 d = i.Point.position - p;
            d.y = 0f;
            if (d.sqrMagnitude > best) continue;
            best = d.sqrMagnitude;
            closest = i;
        }
        return closest;
    }

    private void OnGUI()
    {
        if (Current == null) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height * 0.85f), Current.Prompt, style);
    }
}
