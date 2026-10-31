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
    [SerializeField] private Animator animator;   // optionnel

    [SerializeField] private PlayerMode startMode = PlayerMode.Walking;

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
        if (animator == null) animator = GetComponent<Animator>();
        climbingHands = GetComponent<ClimbingHands>();
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
            SetMode(PlayerMode.Walking);   // toujours autorisé //TODO: Here we have to forbid when it's in the middle of a voie
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
        CurrentMode = mode;

        if (mode == PlayerMode.Walking) ApplyWalking();
        else ApplyClimbing();

        Debug.Log($"Mode joueur : {mode}");
    }

    private void ApplyWalking()
    {
        // Ordre : d'abord couper ClimbLook (il remet le pitch d'entrée), puis réactiver la marche.
        SetEnabled(climbLook, false);
        SetEnabled(characterController, true);
        SetEnabled(animator, true);
        SetEnabled(walkController, true);
    }

    private void ApplyClimbing()
    {
        SetEnabled(walkController, false);
        SetEnabled(characterController, false);
        SetEnabled(animator, false);
        SetEnabled(climbLook, true);
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
