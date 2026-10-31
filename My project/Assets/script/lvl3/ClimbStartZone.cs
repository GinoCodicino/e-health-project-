using UnityEngine;

/// <summary>
/// Trigger au pied de la paroi. Indique à ClimbingHands si le joueur est dans la zone de départ.
/// Le Collider de cet objet doit avoir "Is Trigger" coché.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClimbStartZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other) => SetInZone(other, true);
    private void OnTriggerExit(Collider other) => SetInZone(other, false);

    private static void SetInZone(Collider other, bool value)
    {
        var hands = other.GetComponentInParent<ClimbingHands>();
        if (hands != null) hands.inStartZone = value;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
