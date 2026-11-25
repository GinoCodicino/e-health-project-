using System.Collections;
using UnityEngine;

/// <summary>
/// A piece of litter on the summit: press E nearby to pick it up.
/// It shrinks away and tells the SummitTrashCounter.
/// </summary>
public class TrashPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "can";
    [SerializeField] private float pickupDuration = 0.25f;

    private bool pickedUp;

    public string Prompt => $"Press E to pick up the {displayName}";
    public Transform Point => transform;
    public bool CanInteract => isActiveAndEnabled && !pickedUp;
    public bool PickedUp => pickedUp;

    private void OnEnable()
    {
        PlayerInteractor.Register(this);
        SummitTrashCounter.Register(this);
    }

    private void OnDisable() => PlayerInteractor.Unregister(this);

    public void Interact(PlayerInteractor interactor)
    {
        if (pickedUp) return;
        pickedUp = true;
        SummitTrashCounter.NotifyPickedUp(this);
        StartCoroutine(Vanish());
    }

    private IEnumerator Vanish()
    {
        Vector3 start = transform.localScale;
        for (float t = 0f; t < pickupDuration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(start, Vector3.zero, t / pickupDuration);
            transform.position += Vector3.up * Time.deltaTime;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
