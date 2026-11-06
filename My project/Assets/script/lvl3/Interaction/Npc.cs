using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A character the player can talk to: press E nearby to play its dialogue.
/// Turns to face the player while talking.
/// </summary>
public class Npc : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "Ethan Brooks";
    [SerializeField] private DialogueData dialogue;

    [Header("Facing")]
    [SerializeField] private bool turnToPlayer = true;
    [Tooltip("Correction if the model doesn't face its forward axis (degrees).")]
    [SerializeField] private float modelYawOffset = 0f;
    [SerializeField] private float turnSpeed = 360f;

    [Tooltip("Called at the end of the dialogue (give an item, unlock a path...).")]
    public UnityEvent onDialogueFinished;

    public string Prompt => $"Press E to talk to {displayName}";
    public Transform Point => transform;
    public bool CanInteract => isActiveAndEnabled && dialogue != null;

    private Transform lookTarget;

    private void OnEnable() => PlayerInteractor.Register(this);
    private void OnDisable() => PlayerInteractor.Unregister(this);

    public void Interact(PlayerInteractor interactor)
    {
        if (turnToPlayer) lookTarget = interactor.transform;
        DialogueRunner.Play(dialogue, displayName, () =>
        {
            lookTarget = null;
            onDialogueFinished?.Invoke();
        });
    }

    private void Update()
    {
        if (lookTarget == null) return;
        Vector3 d = lookTarget.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(d) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }
}
