using UnityEngine;
using UnityEngine.InputSystem;   // le projet utilise le nouvel Input System

public class ClimbingHands : MonoBehaviour
{
    public LayerMask holdMask;        // à régler sur "Hold" dans l'Inspector
    public float maxDistance = 3f;    // portée du regard

    // Mis à jour par ClimbStartZone
    [HideInInspector] public bool inStartZone;

    Camera cam;
    Hold current;                     // la prise visée en ce moment
    PlayerModeController modeController;

    void Start()
    {
        cam = Camera.main;
        modeController = GetComponent<PlayerModeController>();
        if (modeController == null)
            Debug.LogWarning("ClimbingHands : pas de PlayerModeController sur le joueur, le passage en escalade est impossible.");
    }

    void OnDisable()
    {
        SetCurrent(null);
    }

    void SetCurrent(Hold aimed)
    {
        if (aimed == current) return;
        if (current != null) current.SetHighlight(false);
        if (aimed != null) aimed.SetHighlight(true);
        current = aimed;
    }

    // Rayon depuis le centre de l'écran (= le réticule)
    Hold RaycastHold()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, holdMask, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponent<Hold>();
        return null;
    }

    // Vrai si une prise est visée en ce moment (indépendant de la zone de départ)
    public bool HasHoldInSight() => RaycastHold() != null;

    bool IsWalking => modeController == null || modeController.CurrentMode == PlayerMode.Walking;

    // En marche, les mains ne sont actives que dans la zone de départ.
    bool CanUseHands => !IsWalking || inStartZone;

    void Update()
    {
        if (!CanUseHands)
        {
            SetCurrent(null);
            return;
        }

        SetCurrent(RaycastHold());
        HandleClicks();
    }

    void HandleClicks()
    {
        var mouse = Mouse.current;
        if (current == null || mouse == null) return;

        bool left = mouse.leftButton.wasPressedThisFrame;
        bool right = mouse.rightButton.wasPressedThisFrame;
        if (!left && !right) return;

        EnterClimbingMode();

        if (left) GrabWith("gauche");
        if (right) GrabWith("droite");
    }

    // Premier clic en marche : on passe en escalade
    void EnterClimbingMode()
    {
        if (IsWalking && modeController != null)
            modeController.SetMode(PlayerMode.Climbing);
    }

    void GrabWith(string hand)
    {
        Debug.Log($"Main {hand} sur {current.name}");
    }
}
