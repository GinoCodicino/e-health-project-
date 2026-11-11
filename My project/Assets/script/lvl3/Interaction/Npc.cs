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

    [Tooltip("Called at the end of the dialogue (give an item, unlock a path...).")]
    public UnityEvent onDialogueFinished;

    public string Prompt => $"Press E to talk to {displayName}";
    public Transform Point => transform;
    public bool CanInteract => isActiveAndEnabled && dialogue != null;

    private void OnEnable() => PlayerInteractor.Register(this);
    private void OnDisable() => PlayerInteractor.Unregister(this);

    public void Interact(PlayerInteractor interactor)
    {
        DialogueRunner.Play(dialogue, displayName, onDialogueFinished.Invoke, () => FaceEachOther(interactor.transform));
    }

    // Called while the screen is black: the player stands in front of the NPC, on the side he came from.
    private void FaceEachOther(Transform player)
    {
        Vector3 away = player.position - transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;

        float playerYaw = Quaternion.LookRotation(-away).eulerAngles.y;
        PlayerPlacement.PlaceOnGround(player, transform.position + away * talkDistance, playerYaw);
        var controller = player.GetComponent<playercontroller>();
        if (controller != null) controller.SetPitch(0f);

        if (turnToPlayer)
            transform.rotation = Quaternion.LookRotation(away) * Quaternion.Euler(0f, modelYawOffset, 0f);
    }
}
