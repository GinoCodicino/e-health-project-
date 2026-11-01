using System;
using UnityEngine;
using UnityEngine.InputSystem;   // le projet utilise le nouvel Input System

public class ClimbingHands : MonoBehaviour
{
    [Header("Mains")]
    public Hand leftHand;
    public Hand rightHand;

    [Header("Visée")]
    public LayerMask holdMask;        // à régler sur "Hold" dans l'Inspector
    public float maxDistance = 15f;    // portée du regard
    [Tooltip("Distance max entre la caméra et la prise pour l'attraper.")]
    public float armReach = 10f;

    [Header("Voie")]
    [Tooltip("La voie (graphe des prises). Si vide : toutes les prises sont attrapables.")]
    public Route route;

    /// <summary>Levé quand les deux mains sont libres alors qu'on était en escalade.</summary>
    public event Action OnAllHandsFree;

    Camera cam;
    Hold current;                     // la prise visée en ce moment
    PlayerModeController modeController;
    bool summitReached;               // pour n'annoncer la victoire qu'une fois

    void Start()
    {
        cam = Camera.main;
        modeController = GetComponent<PlayerModeController>();
        if (modeController == null)
            Debug.LogWarning("ClimbingHands : pas de PlayerModeController sur le joueur, le passage en escalade est impossible.");
        if (leftHand == null || rightHand == null)
            Debug.LogError("ClimbingHands : leftHand / rightHand non assignés.", this);
        if (route == null)
            Debug.LogWarning("ClimbingHands : pas de Route assignée, les règles de voie sont désactivées.", this);
    }

    void OnEnable()
    {
        Subscribe(leftHand);
        Subscribe(rightHand);
    }

    void OnDisable()
    {
        Unsubscribe(leftHand);
        Unsubscribe(rightHand);
        SetCurrent(null);
    }

    void Subscribe(Hand h)   { if (h != null) h.OnReleased += HandReleased; }
    void Unsubscribe(Hand h) { if (h != null) h.OnReleased -= HandReleased; }

    void HandReleased(Hand _)
    {
        if (!AnyHandHolding && !IsWalking)
            OnAllHandsFree?.Invoke();
    }

    public bool AnyHandHolding =>
        (leftHand != null && leftHand.IsHolding) || (rightHand != null && rightHand.IsHolding);

    public void ReleaseAll()
    {
        if (leftHand != null) leftHand.Release(false);
        if (rightHand != null) rightHand.Release(false);
    }

    /// <summary>Centre des prises tenues (moyenne des mains accrochées).</summary>
    public bool TryGetHoldsCenter(out Vector3 center)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        if (leftHand != null && leftHand.IsHolding)   { sum += leftHand.CurrentHold.transform.position; n++; }
        if (rightHand != null && rightHand.IsHolding) { sum += rightHand.CurrentHold.transform.position; n++; }
        center = n > 0 ? sum / n : Vector3.zero;
        return n > 0;
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

    // Les mains ne servent qu'en mode escalade (on y entre avec la touche C).
    bool CanUseHands => !IsWalking;

    bool InReach(Hold h) =>
        cam != null && Vector3.Distance(cam.transform.position, h.transform.position) <= armReach;

    // ---------- Règles de la voie ----------

    Hand Other(Hand hand) => hand == leftHand ? rightHand : leftHand;

    /// <summary>
    /// Pourquoi cette main ne peut pas attraper cette prise ? null = elle peut.
    /// </summary>
    string WhyCannotGrab(Hand hand, Hold target)
    {
        if (hand == null || target == null) return "pas de main ou pas de prise";
        if (target.IsTaken) return "prise déjà occupée";
        if (!InReach(target)) return "prise hors de portée";
        if (route == null) return null;                       // pas de voie : pas de règle

        // Aucune main ne tient : on ne peut commencer que par une prise de départ.
        if (!AnyHandHolding)
            return target.isStart ? null : "il faut commencer par une prise de départ";

        // Sinon : la cible doit être une voisine de la prise tenue par l'AUTRE main.
        Hand other = Other(hand);
        if (other == null || !other.IsHolding)
            return "l'autre main doit tenir une prise";
        if (!other.CurrentHold.Next.Contains(target))
            return "prise trop loin de celle tenue par l'autre main";

        return null;
    }

    public bool CanGrab(Hand hand, Hold target) => WhyCannotGrab(hand, target) == null;

    // Une prise visée s'allume si on peut la lâcher (on la tient) ou l'attraper.
    bool IsUsable(Hold h) =>
        h != null && (h.IsTaken || CanGrab(leftHand, h) || CanGrab(rightHand, h));

    void Update()
    {
        if (!CanUseHands)
        {
            SetCurrent(null);
            return;
        }

        Hold aimed = RaycastHold();
        SetCurrent(IsUsable(aimed) ? aimed : null);
        HandleClicks();
    }

    void HandleClicks()
    {
        var mouse = Mouse.current;
        if (current == null || mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)  Click(leftHand);
        if (mouse.rightButton.wasPressedThisFrame) Click(rightHand);
    }

    void Click(Hand hand)
    {
        if (hand == null || current == null) return;

        // Clic sur la prise qu'on tient déjà : on lâche.
        if (hand.CurrentHold == current)
        {
            hand.Release();
            return;
        }

        string reason = WhyCannotGrab(hand, current);
        if (reason != null)
        {
            Debug.Log($"Impossible d'attraper {current.name} : {reason}");
            return;
        }

        hand.Grab(current);

        if (current.isSummit && !summitReached)
        {
            summitReached = true;
            Debug.Log("Sommet atteint : victoire !");
        }
    }
}
