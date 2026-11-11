using UnityEngine;

public class PlayerFPS : MonoBehaviour
{
    [Header("Modalità adattatore (per personaggi con un controller proprio)")]
    [Tooltip("Spunta se il personaggio ha già il suo controller: questo script non muove niente, blocca soltanto i componenti indicati qui sotto durante dialoghi e minigiochi")]
    public bool soloBlocco = false;
    public Behaviour[] componentiDaBloccare;

    [Header("Movimento (ignorato con Solo Blocco)")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float gravity = -20f;
    public float jumpHeight = 0.9f;

    [Header("Sguardo (ignorato con Solo Blocco)")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Riferimenti")]
    public Transform cameraTransform;   // se vuoto, usa Camera.main

    bool puoMuoversi = true;

    // Lo usano dialoghi e minigiochi per bloccare/sbloccare il giocatore
    public bool canMove
    {
        get { return puoMuoversi; }
        set
        {
            puoMuoversi = value;
            if (componentiDaBloccare == null) return;
            foreach (Behaviour b in componentiDaBloccare)
                if (b != null) b.enabled = value;
        }
    }

    CharacterController controller;
    float verticalVelocity;
    float pitch;

    void Start()
    {
        if (soloBlocco) return;

        controller = GetComponent<CharacterController>();
        if (controller == null) controller = gameObject.AddComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        LockCursor(true);
    }

    void Update()
    {
        if (soloBlocco) return;
        if (!canMove) return;

        if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

        Look();
        Move();
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mx, 0f);

        pitch = Mathf.Clamp(pitch - my, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * x + transform.forward * z);
        if (move.sqrMagnitude > 1f) move.Normalize();

        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}