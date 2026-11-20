using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(playerinputcontroller))]
public class playercontroller : MonoBehaviour
{
    [Header("Modalità adattatore")]
    [Tooltip("Attivalo se il personaggio ha già il suo controller. Questo script non muoverà nulla, ma potrà bloccare i componenti indicati durante dialoghi e minigiochi.")]
    [SerializeField] private bool soloBlocco = false;

    [Tooltip("Componenti da disattivare quando canMove è false, ad esempio PlayerInput, script di interazione, arma, ecc.")]
    [SerializeField] private Behaviour[] componentiDaBloccare;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;

    [Header("Jump and gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float maxLookAngle = 80f;

    private playerinputcontroller inputController;
    private CharacterController characterController;

    private float cameraPitch;
    private float verticalVelocity;
    private bool cursorReleased;
    private bool hasFocus = true;
    private int ignoreLookUntilFrame;
    private readonly HashSet<Object> uiOwners = new HashSet<Object>();

    // Blocco usato dai minigiochi del livello 1 (dialoghi, QTE, memory...).
    // Ferma movimento e sguardo senza toccare il cursore: lo gestisce il minigioco.
    private bool puoMuoversi = true;

    public bool canMove
    {
        get => puoMuoversi;
        set
        {
            puoMuoversi = value;
            if (!value && inputController != null) inputController.ClearInput();

            if (componentiDaBloccare == null)
                return;

            foreach (Behaviour componente in componentiDaBloccare)
            {
                if (componente != null)
                    componente.enabled = value;
            }
        }
    }

    // Blocco usato dai pannelli UI del livello 2 (SetUiMode).
    public bool IsGameplayBlocked { get { return uiOwners.Count > 0 || cursorReleased || !hasFocus; } }

    private void Awake()
    {
        inputController = GetComponent<playerinputcontroller>();
        characterController = GetComponent<CharacterController>();
        if (pitchPivot == null) pitchPivot = transform.Find("pitchpivot");
        if (pitchPivot == null)
        {
            Camera view = GetComponentInChildren<Camera>(true);
            if (view != null)
                pitchPivot = view.transform.parent != transform ? view.transform.parent : view.transform;
        }
        if (pitchPivot != null) cameraPitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
    }

    private void Start()
    {
        if (pitchPivot == null && Camera.main != null)
        {
            pitchPivot = Camera.main.transform;
            cameraPitch = Mathf.DeltaAngle(0f, pitchPivot.localEulerAngles.x);
        }

        ApplyInputMode();
    }

    // Ogni pannello possiede il proprio blocco: chiuderne uno non sblocca gli altri.
    public void SetUiMode(Object owner, bool open)
    {
        if (owner == null) return;
        if (open) uiOwners.Add(owner);
        else uiOwners.Remove(owner);
        cursorReleased = false;
        ignoreLookUntilFrame = Time.frameCount + 1;
        ApplyInputMode();
    }

    private void ApplyInputMode()
    {
        if (inputController == null) return;
        inputController.SetGameplayInputEnabled(!IsGameplayBlocked);
        inputController.ClearInput();
        Cursor.lockState = IsGameplayBlocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = IsGameplayBlocked;
    }

    private void Update()
    {
        if (soloBlocco)
            return;

        if (!canMove)
            return;

        if (uiOwners.Count == 0)
        {
            if (playerinputcontroller.ConsumeEscape())
            {
                cursorReleased = !cursorReleased;
                ignoreLookUntilFrame = Time.frameCount + 1;
                ApplyInputMode();
            }
            else if (cursorReleased && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                cursorReleased = false;
                ignoreLookUntilFrame = Time.frameCount + 1;
                ApplyInputMode();
            }
        }

        if (!hasFocus) return;
        if (!IsGameplayBlocked && Time.frameCount > ignoreLookUntilFrame) HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (!characterController.enabled) return;
        bool grounded = characterController.isGrounded;

        // Mantiene il contatto con il terreno quando non saltiamo.
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

        Vector2 input = IsGameplayBlocked ? Vector2.zero : inputController.movementInputVector;

        // Evita una velocità maggiore nelle diagonali,
        // conservando l'intensità di un eventuale stick analogico.
        Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
        float speed = !IsGameplayBlocked && inputController.sprintHeld ? sprintSpeed : moveSpeed;
        bool jumpPressed = inputController.ConsumeJumpRequest();
        if (!IsGameplayBlocked && jumpPressed && grounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;
        CollisionFlags collisions = characterController.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Se colpiamo un soffitto, interrompiamo la salita.
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
    }

    /// <summary>Sets the camera's vertical angle (0 = horizontal), e.g. to face an NPC.</summary>
    public void SetPitch(float pitch)
    {
        cameraPitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        if (pitchPivot != null)
        {
            pitchPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleLook()
    {
        Vector2 look = inputController.lookInputVector;
        transform.Rotate(0f, look.x * mouseSensitivity, 0f);
        cameraPitch = Mathf.Clamp(cameraPitch - look.y * mouseSensitivity, -maxLookAngle, maxLookAngle);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void OnApplicationFocus(bool focus)
    {
        hasFocus = focus;
        ignoreLookUntilFrame = Time.frameCount + 1;

        // Durante un minigioco del livello 1 il cursore appartiene al minigioco.
        if (canMove && !soloBlocco) ApplyInputMode();
    }

    private void OnDisable()
    {
        if (inputController != null) inputController.ClearInput();
        uiOwners.Clear();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
