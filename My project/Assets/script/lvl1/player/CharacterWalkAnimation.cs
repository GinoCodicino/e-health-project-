using UnityEngine;

[RequireComponent(typeof(Animator))]
public class CharacterWalkAnimation : MonoBehaviour
{
    [SerializeField] private float minimumWalkSpeed = 0.05f;

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
    }

    private void LateUpdate()
    {
        Vector3 displacement = transform.position - previousPosition;

        // Non considerare salti o cadute come camminata.
        displacement.y = 0f;

        float speed = displacement.magnitude
                    / Mathf.Max(Time.deltaTime, 0.000001f);

        animator.SetBool(IsWalking, speed > minimumWalkSpeed);

        previousPosition = transform.position;
    }
}
