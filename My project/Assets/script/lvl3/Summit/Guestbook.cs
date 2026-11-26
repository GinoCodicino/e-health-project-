using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// The summit guestbook, closed on the table. Click on it (or press E nearby) to open it:
/// the front cover swings open around the spine.
/// Locked until all the litter on the summit is picked up (SummitTrashCounter).
/// onOpened is called once the book is open (to start the grandfather's flashback).
/// </summary>
public class Guestbook : MonoBehaviour, IInteractable
{
    [Tooltip("The front cover. Its pivot must be on the hinge (spine edge).")]
    [SerializeField] private Transform cover;
    [Tooltip("Local axis of the cover the hinge turns around.")]
    [SerializeField] private Vector3 hingeAxis = Vector3.right;
    [Tooltip("How far the cover opens (degrees). A bit over 180 so it rests on the table.")]
    [SerializeField] private float openAngle = 193f;
    [SerializeField] private float openDuration = 1.2f;

    [Header("Interaction")]
    [Tooltip("Max distance from the camera to click on the book (m).")]
    [SerializeField] private float clickRange = 3f;
    [Tooltip("The book only opens once the summit is clean.")]
    [SerializeField] private bool requireCleanSummit = true;
    [SerializeField] private string openPrompt = "Click or press E to open the guestbook";
    [SerializeField] private string lockedPrompt = "Clean up the summit first";

    [Tooltip("Called once the book is open (start the flashback...).")]
    public UnityEvent onOpened;

    public bool IsOpen { get; private set; }

    public string Prompt => Locked ? lockedPrompt : openPrompt;
    public Transform Point => transform;
    public bool CanInteract => isActiveAndEnabled && !IsOpen && !opening;

    private bool Locked => requireCleanSummit && !SummitTrashCounter.AllCollected;

    private bool opening;
    private Quaternion closedRotation;
    private Collider[] colliders;

    private void Awake()
    {
        if (cover != null) closedRotation = cover.localRotation;
        colliders = GetComponentsInChildren<Collider>();
    }

    private void OnEnable() => PlayerInteractor.Register(this);
    private void OnDisable() => PlayerInteractor.Unregister(this);

    public void Interact(PlayerInteractor interactor) => TryOpen();

    private void Update()
    {
        if (!CanInteract || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (IsClicked()) TryOpen();
    }

    // Ray from the camera: screen center when the cursor is locked (first person), else the mouse position.
    private bool IsClicked()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector2 screen = Cursor.lockState == CursorLockMode.Locked
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screen);
        foreach (Collider c in colliders)
            if (c.Raycast(ray, out _, clickRange)) return true;
        return false;
    }

    private void TryOpen()
    {
        if (!CanInteract || Locked) return;
        StartCoroutine(Open());
    }

    private IEnumerator Open()
    {
        opening = true;
        if (cover != null)
        {
            // Angle interpolated by hand: Slerp would take the short way round past 180 degrees.
            for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
            {
                SetCoverAngle(openAngle * Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            SetCoverAngle(openAngle);
        }
        opening = false;
        IsOpen = true;
        onOpened.Invoke();
    }

    private void SetCoverAngle(float angle) =>
        cover.localRotation = closedRotation * Quaternion.AngleAxis(angle, hingeAxis);
}
