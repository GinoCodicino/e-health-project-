using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterWalkAnimation : MonoBehaviour
{
    [Header("Impostazioni")]
    [SerializeField] private float minimumWalkSpeed = 0.05f;

    [Header("Debug - osserva questi valori durante Play")]
    [SerializeField] private float horizontalSpeed;
    [SerializeField] private bool walkingDetected;

    private Animator animator;
    private Vector3 previousPosition;

    private static readonly int IsWalking =
        Animator.StringToHash("isWalking");

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        horizontalSpeed = 0f;
        walkingDetected = false;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        Vector3 displacement = currentPosition - previousPosition;

        // Ignora lo spostamento verticale.
        displacement.y = 0f;

        horizontalSpeed = Time.deltaTime > 0f
            ? displacement.magnitude / Time.deltaTime
            : 0f;

        walkingDetected = horizontalSpeed > minimumWalkSpeed;

        animator.SetBool(IsWalking, walkingDetected);

        previousPosition = currentPosition;
    }
}
