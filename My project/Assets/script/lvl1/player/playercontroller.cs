using UnityEngine;

public class playercontroller : MonoBehaviour
{
    private playerinputcontroller inputController;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Look")]
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float maxLookAngle = 80f;

    private float cameraPitch = 0f;

    private void Awake()
    {
        inputController = GetComponent<playerinputcontroller>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
{
    Vector2 input = inputController.movementinputvector;

    Vector3 move = transform.right * input.x + transform.forward * input.y;
    move = move.normalized;

    transform.position += move * moveSpeed * Time.deltaTime;
}


    private void HandleLook()
{
    Vector2 look = inputController.lookInputVector;

    float mouseX = look.x * mouseSensitivity;
    float mouseY = look.y * mouseSensitivity;

    transform.Rotate(0f, mouseX, 0f);

    cameraPitch -= mouseY;
    cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

    pitchPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
}

}
