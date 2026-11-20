using UnityEngine;

/// <summary>
/// Procedural "scheming" idle for Ethan Brooks (cubic character without Animator):
/// he rubs his hands, leans forward with hunched shoulders, glances around,
/// chuckles now and then, and follows the player with his head when they come close.
/// Bones are found by name if the fields are left empty.
/// </summary>
public class EthanBrooksAnimator : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Hand rubbing (spine space)")]
    [Tooltip("Where the hands meet, in front of the chest.")]
    [SerializeField] private Vector3 handsCenter = new Vector3(0f, 1.5f, 1.6f);
    [Tooltip("Distance between the two hands.")]
    [SerializeField] private float handsGap = 1.1f;
    [Tooltip("Size of the rubbing motion.")]
    [SerializeField] private float rubAmplitude = 0.3f;
    [Tooltip("Rubs per second.")]
    [SerializeField] private float rubSpeed = 2.2f;
    [Tooltip("Seconds of rubbing, then seconds of pause with the hands together.")]
    [SerializeField] private float rubDuration = 2.6f;
    [SerializeField] private float rubPause = 1.6f;

    [Header("Posture")]
    [Tooltip("Lean forward (degrees).")]
    [SerializeField] private float leanForward = 8f;
    [Tooltip("Shoulders pulled down (spine space).")]
    [SerializeField] private float hunch = 0.12f;
    [Tooltip("Head lowered, as if looking from under his brows (degrees).")]
    [SerializeField] private float headDown = 4f;
    [Tooltip("Head tilted to the side (degrees).")]
    [SerializeField] private float headTilt = 7f;
    [Tooltip("Breathing amplitude (spine space).")]
    [SerializeField] private float breathing = 0.04f;

    [Header("Chuckle")]
    [Tooltip("Seconds between two chuckles.")]
    [SerializeField] private float chuckleEvery = 7f;
    [SerializeField] private float chuckleDuration = 1f;

    [Header("Looking around / at the player")]
    [Tooltip("Head turns left and right when nobody is around (degrees).")]
    [SerializeField] private float glanceAngle = 30f;
    [Tooltip("Distance at which he starts to follow the player with his head.")]
    [SerializeField] private float lookRange = 12f;
    [SerializeField] private float maxHeadYaw = 60f;
    [SerializeField] private float lookSpeed = 4f;
    [Tooltip("Player to watch. Empty = the playercontroller in the scene.")]
    [SerializeField] private Transform player;

    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;
    private float headYaw;
    private float lookWeight;
    private float timeOffset;

    private void Awake()
    {
        FindBones();
        CaptureRest();
        // Chaque instance démarre à un moment différent du cycle.
        timeOffset = Random.Range(0f, 10f);
    }

    private void Start()
    {
        if (player == null)
        {
            var controller = FindAnyObjectByType<playercontroller>();
            if (controller != null) player = controller.transform;
        }
    }

    private void LateUpdate()
    {
        // Suivi du joueur : poids selon la distance, angle lissé.
        float targetWeight = 0f;
        float targetYaw = 0f;
        if (player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < lookRange && distance > 0.01f)
            {
                Vector3 local = transform.InverseTransformDirection(toPlayer / distance);
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);
                targetWeight = Mathf.InverseLerp(lookRange, lookRange * 0.6f, distance);
            }
        }
        float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);
        lookWeight = Mathf.Lerp(lookWeight, targetWeight, k);
        headYaw = Mathf.Lerp(headYaw, targetYaw, k);

        Evaluate(Time.time + timeOffset, headYaw, lookWeight);
    }

    /// <summary>Finds the bones of the cubic character rig by name.</summary>
    public void FindBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (spine == null && t.name == "Spine00") spine = t;
            else if (head == null && t.name == "Head") head = t;
            else if (handLeft == null && t.name == "Arm_L") handLeft = t;
            else if (handRight == null && t.name == "Arm_R") handRight = t;
        }
    }

    /// <summary>Stores the current pose as the rest pose.</summary>
    public void CaptureRest()
    {
        if (spine == null || head == null || handLeft == null || handRight == null) return;
        spineRestPos = spine.localPosition; spineRestRot = spine.localRotation;
        headRestPos = head.localPosition; headRestRot = head.localRotation;
        leftRestPos = handLeft.localPosition; leftRestRot = handLeft.localRotation;
        rightRestPos = handRight.localPosition; rightRestRot = handRight.localRotation;
        restCaptured = true;
    }

    /// <summary>Puts the bones back in the rest pose.</summary>
    public void RestoreRest()
    {
        if (!restCaptured) return;
        spine.localPosition = spineRestPos; spine.localRotation = spineRestRot;
        head.localPosition = headRestPos; head.localRotation = headRestRot;
        handLeft.localPosition = leftRestPos; handLeft.localRotation = leftRestRot;
        handRight.localPosition = rightRestPos; handRight.localRotation = rightRestRot;
    }

    /// <summary>Poses the character at time t. playerYaw/playerWeight: head turn toward the player.</summary>
    public void Evaluate(float t, float playerYaw, float playerWeight)
    {
        if (!restCaptured) return;

        // Respiration et ricanement (petits sauts des épaules, tête en arrière).
        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.28f);
        float chuckle = 0f, chuckleHead = 0f;
        float c = Mathf.Repeat(t, chuckleEvery);
        if (c < chuckleDuration)
        {
            float u = c / chuckleDuration;
            float envelope = Mathf.Sin(u * Mathf.PI);
            chuckle = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5f)) * envelope;
            chuckleHead = envelope;
        }

        // Torse : penché en avant, épaules rentrées.
        spine.localPosition = spineRestPos + new Vector3(0f, -hunch + breath * breathing + chuckle * 0.09f, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(leanForward + breath * 1.2f - chuckleHead * 4f, 0f, 0f);

        // Tête : baissée et penchée, coups d'œil furtifs, puis le joueur.
        float glance = (Mathf.PerlinNoise(t * 0.35f, 7.3f) - 0.5f) * 2f * glanceAngle;
        float yaw = Mathf.Lerp(glance, playerYaw, playerWeight);
        float pitch = headDown - chuckleHead * 14f;
        float roll = headTilt * (1f - playerWeight * 0.4f) + Mathf.Sin(t * 0.6f) * 2f;
        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, yaw, roll);

        // Mains : se frottent par séries, puis restent jointes un instant.
        float cycle = rubDuration + rubPause;
        float inCycle = Mathf.Repeat(t, cycle);
        float rubWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inCycle / 0.3f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rubDuration - inCycle) / 0.3f));
        float phase = t * Mathf.PI * 2f * rubSpeed;
        Vector3 rub = new Vector3(0f, Mathf.Sin(phase) * 0.5f, Mathf.Cos(phase)) * rubAmplitude * rubWeight;
        Vector3 bob = new Vector3(0f, breath * breathing + chuckle * 0.12f, 0f);
        handLeft.localPosition = handsCenter + new Vector3(-handsGap * 0.5f, 0f, 0f) + rub + bob;
        handRight.localPosition = handsCenter + new Vector3(handsGap * 0.5f, 0f, 0f) - rub + bob;
        float twist = Mathf.Sin(phase) * 18f * rubWeight;
        handLeft.localRotation = leftRestRot * Quaternion.Euler(twist, 0f, 0f);
        handRight.localRotation = rightRestRot * Quaternion.Euler(-twist, 0f, 0f);
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}
