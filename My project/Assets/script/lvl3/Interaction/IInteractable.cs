using UnityEngine;

/// <summary>
/// Something the player can interact with (NPC, door, sign...).
/// Implementations register themselves with PlayerInteractor.Register in OnEnable.
/// </summary>
public interface IInteractable
{
    /// <summary>Shown on screen when this is the closest interactable, e.g. "Press E to talk to Ethan".</summary>
    string Prompt { get; }
    /// <summary>Position used to measure the distance to the player.</summary>
    Transform Point { get; }
    bool CanInteract { get; }
    void Interact(PlayerInteractor interactor);
}
