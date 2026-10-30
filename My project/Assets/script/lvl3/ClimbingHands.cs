using UnityEngine;
using UnityEngine.InputSystem;   // le projet utilise le nouvel Input System

public class ClimbingHands : MonoBehaviour
{
    public LayerMask holdMask;        // à régler sur "Hold" dans l'Inspector
    public float maxDistance = 3f;    // portée du regard

    Camera cam;
    Hold current;                     // la prise visée en ce moment

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        // 1. Lancer un rayon depuis le centre de l'écran (= le réticule)
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Hold aimed = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, holdMask, QueryTriggerInteraction.Ignore))
            aimed = hit.collider.GetComponent<Hold>();

        // 2. Si la prise visée a changé : éteindre l'ancienne, allumer la nouvelle
        if (aimed != current)
        {
            if (current != null) current.SetHighlight(false);
            if (aimed != null) aimed.SetHighlight(true);
            current = aimed;
        }

        // 3. Clic gauche ou droit sur la prise visée
        if (current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Debug.Log($"Main gauche sur {current.name}");
        if (current != null && Mouse.current.rightButton.wasPressedThisFrame)
            Debug.Log($"Main droite sur {current.name}");
    }
}