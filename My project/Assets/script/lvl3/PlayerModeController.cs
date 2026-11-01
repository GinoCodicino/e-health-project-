using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerMode { Walking, Climbing }

/// <summary>
/// Bascule le joueur entre marche et escalade en activant/désactivant les composants.
/// À placer sur l'instance du joueur dans Level3_grandpa uniquement.
/// </summary>
public class PlayerModeController : MonoBehaviour
{
    [Header("Composants (auto-remplis depuis ce GameObject si vides)")]
    [SerializeField] private playercontroller walkController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private ClimbLook climbLook;
    [SerializeField] private ClimbBody climbBody;
    [SerializeField] private Animator animator;   // optionnel

    [SerializeField] private PlayerMode startMode = PlayerMode.Walking;

    [Header("Chute")]
    [Tooltip("Point au pied de la voie où le joueur retombe quand il quitte l'escalade.")]
    [SerializeField] private Transform fallRespawn;

    private ClimbingHands climbingHands;

    public PlayerMode CurrentMode { get; private set; }

    private void Awake()
    {
        FindComponents();
    }

    // Récupère les composants du joueur sur ce GameObject.
    // Ceux déjà assignés dans l'Inspector sont conservés ; seuls les champs vides sont remplis.
    // ClimbingHands n'est pas sérialisé : il est toujours cherché automatiquement.
    private void FindComponents()
    {
        if (walkController == null) walkController = GetComponent<playercontroller>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (climbLook == null) climbLook = GetComponent<ClimbLook>();
        if (climbBody == null) climbBody = GetComponent<ClimbBody>();
        if (animator == null) animator = GetComponent<Animator>();
        climbingHands = GetComponent<ClimbingHands>();
    }

    private void OnEnable()
    {
        if (climbingHands == null) climbingHands = GetComponent<ClimbingHands>();
        if (climbingHands != null) climbingHands.OnAllHandsFree += HandleAllHandsFree;
    }

    private void OnDisable()
    {
        if (climbingHands != null) climbingHands.OnAllHandsFree -= HandleAllHandsFree;
    }

    // Plus aucune prise tenue : on tombe (le CharacterController réactivé applique la gravité).
    private void HandleAllHandsFree()
    {
        if (CurrentMode == PlayerMode.Climbing) SetMode(PlayerMode.Walking);
    }

    private void Start()
    {
        SetMode(startMode);
    }

    private void Update()
    {
        if (IsTogglePressed()) ToggleMode();
    }

    // Touche C : bascule entre marche et escalade.
    private static bool IsTogglePressed()
    {
        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
    }

    private bool HoldInSight => climbingHands != null && climbingHands.HasHoldInSight();

    private void ToggleMode()
    {
        if (CurrentMode == PlayerMode.Climbing)
            SetMode(PlayerMode.Walking);   // toujours autorisé : lâche les prises et tombe
        else
            TryStartClimbing();
    }

    private void TryStartClimbing()
    {
        if (!HoldInSight)
        {
            Debug.Log("Aucune prise en vue : escalade refusée");
            return;
        }
        SetMode(PlayerMode.Climbing);
    }

    public void SetMode(PlayerMode mode)
    {
        bool wasClimbing = CurrentMode == PlayerMode.Climbing;
        CurrentMode = mode;

        if (mode == PlayerMode.Walking) ApplyWalking(wasClimbing);
        else ApplyClimbing();

        Debug.Log($"Mode joueur : {mode}");
    }

    private void ApplyWalking(bool wasClimbing)
    {
        // Ordre : d'abord couper ClimbLook (il remet le pitch d'entrée), puis réactiver la marche.
        SetEnabled(climbLook, false);
        SetEnabled(climbBody, false);
        if (climbingHands != null) climbingHands.ReleaseAll();

        // On quitte l'escalade : on retombe au pied de la voie.
        // À faire AVANT de réactiver le CharacterController, sinon il écrase la position.
        if (wasClimbing) MoveToFallRespawn();

        SetEnabled(characterController, true);
        SetEnabled(animator, true);
        SetEnabled(walkController, true);
    }

    // Place le joueur sur le sol sous fallRespawn. Le sol n'est pas à y = 0 :
    // on le cherche avec un rayon vers le bas (en ignorant les prises et le joueur).
    private void MoveToFallRespawn()
    {
        if (fallRespawn == null) return;

        Vector3 origin = fallRespawn.position + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 target = fallRespawn.position;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponent<Hold>() != null) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            target = hit.point + Vector3.up * 0.05f;
            break;
        }

        transform.position = target;
        transform.rotation = Quaternion.Euler(0f, fallRespawn.eulerAngles.y, 0f);
    }

    private void ApplyClimbing()
    {
        SetEnabled(walkController, false);
        SetEnabled(characterController, false);
        SetEnabled(animator, false);
        SetEnabled(climbLook, true);
        SetEnabled(climbBody, true);
    }

    private static void SetEnabled(Behaviour component, bool value)
    {
        if (component != null) component.enabled = value;
    }

    private static void SetEnabled(Collider component, bool value)
    {
        if (component != null) component.enabled = value;
    }
}
