using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A character the player can talk to: press E nearby to play its dialogue.
/// Short fade to black, then the player and the NPC face each other.
/// </summary>
public class Npc : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "Ethan Brooks";
    [SerializeField] private DialogueData dialogue;

    [Header("Facing")]
    [Tooltip("Distance between the player and the NPC during the dialogue (m).")]
    [SerializeField] private float talkDistance = 1.6f;
    [SerializeField] private bool turnToPlayer = true;
    [Tooltip("Correction if the model doesn't face its forward axis (degrees).")]
    [SerializeField] private float modelYawOffset = 0f;
    [Tooltip("Optional: where the player stands during the dialogue (e.g. in front of a seated NPC). Empty = on the side he came from.")]
    [SerializeField] private Transform talkSpot;
    [Tooltip("Camera pitch of the player during the dialogue (degrees, positive = looking down, for a seated NPC).")]
    [SerializeField] private float talkPitch = 0f;

    [Tooltip("Called at the end of the dialogue (give an item, unlock a path...).")]
    public UnityEvent onDialogueFinished;

    /// <summary>The dialogue with this NPC starts (screen is black, the NPC faces the player).</summary>
    public event System.Action DialogueStarted;
    /// <summary>The dialogue with this NPC is over.</summary>
    public event System.Action DialogueEnded;

    public string DisplayName => displayName;
    public string Prompt => $"Press E to talk to {displayName}";
    public Transform Point => transform;
    public bool CanInteract => isActiveAndEnabled && dialogue != null;

    private void OnEnable() => PlayerInteractor.Register(this);
    private void OnDisable() => PlayerInteractor.Unregister(this);

    public void Interact(PlayerInteractor interactor)
    {
        DialogueRunner.Play(dialogue, displayName, Finish, () => { FaceEachOther(interactor.transform); DialogueStarted?.Invoke(); });
    }

    private void Finish()
    {
        DialogueEnded?.Invoke();
        onDialogueFinished.Invoke();
    }

    // Called while the screen is black: the player stands in front of the NPC, on the side he came from.
    private void FaceEachOther(Transform player)
    {
        Vector3 spot = talkSpot != null ? talkSpot.position : player.position;
        Vector3 away = spot - transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;
        if (talkSpot == null) spot = transform.position + away * talkDistance;

        float playerYaw = Quaternion.LookRotation(-away).eulerAngles.y;
        PlayerPlacement.PlaceOnGround(player, spot, playerYaw);
        var controller = player.GetComponent<playercontroller>();
        if (controller != null) controller.SetPitch(talkPitch);

        if (turnToPlayer)
            transform.rotation = Quaternion.LookRotation(away) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }
}
