using UnityEngine;

/// <summary>
/// Procedural idle for a tired climber sitting on a bench (cubic character without Animator).
/// The seated pose (body on the seat, feet on the ground in front of it) is set in the scene with the
/// component's menu "Sit on the seat", so it shows in the editor too; then he slumps forward with his forearms on his knees, breathes heavily, sighs now and then
/// and looks up at the summit he missed. When the player comes close he looks at him.
/// Works with NpcTalkAnimator on the same object: during a dialogue it gestures on top of this pose.
/// Bones are found by name if the fields are left empty.
/// </summary>
public class ClimberRestAnimator : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;
    [SerializeField] private Transform footLeft;
    [SerializeField] private Transform footRight;

    [Header("Sitting")]
    [Tooltip("Height of the body on the seat, compared with standing (m, positive = up).")]
    [SerializeField] private float sitDrop = -0.28f;
    [Tooltip("Feet moved forward and down to stand on the ground in front of the bench (rig space).")]
    [SerializeField] private Vector3 feetOffset = new Vector3(0f, 1.0f, 2.1f);
    [Tooltip("Feet a bit further apart than when standing.")]
    [SerializeField] private float feetSpread = 0.15f;
    [Tooltip("Set by \"Sit on the seat\": the scene already holds the seated pose.")]
    [SerializeField] private bool seated;

    [Header("Tired posture")]
    [Tooltip("Lean forward (degrees).")]
    [SerializeField] private float slump = 14f;
    [Tooltip("Hands resting on the knees (spine space).")]
    [SerializeField] private Vector3 handsOnKnees = new Vector3(1.0f, 0.5f, 1.2f);
    [Tooltip("Head hanging down (degrees).")]
    [SerializeField] private float headDown = 14f;
    [Tooltip("Breaths per second (out of breath at first, calmer after).")]
    [SerializeField] private float breathRate = 0.45f;
    [SerializeField] private float breathing = 0.07f;

    [Header("Sigh and looking at the summit")]
    [SerializeField] private float sighEvery = 9f;
    [SerializeField] private float sighDuration = 2.2f;
    [Tooltip("Seconds between two looks at the summit.")]
    [SerializeField] private float summitEvery = 13f;
    [SerializeField] private float summitDuration = 3.5f;
    [Tooltip("Where he looks when he looks at the summit. Empty = straight ahead and up.")]
    [SerializeField] private Transform summit;

    [Header("Looking at the player")]
    [SerializeField] private float lookRange = 7f;
    [SerializeField] private float maxHeadYaw = 70f;
    [SerializeField] private float lookSpeed = 3f;
    [Tooltip("Player to watch. Empty = the playercontroller in the scene.")]
    [SerializeField] private Transform player;

    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos, footLeftRestPos, footRightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private Transform rig;
    private Vector3 rigRestPos;
    private bool restCaptured;
    private float headYaw, lookWeight, timeOffset;

    private void Awake()
    {
        FindBones();
        CaptureRest();
        timeOffset = Random.Range(0f, 10f);
        // Not seated in the scene yet: sit down now (the standing pose stays the rest pose).
        if (!seated && restCaptured) ApplySeat(+1f);
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
        float targetWeight = 0f, targetYaw = 0f;
        if (player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < lookRange && distance > 0.01f)
            {
                Vector3 local = transform.InverseTransformDirection(toPlayer / distance);
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);
                targetWeight = Mathf.InverseLerp(lookRange, lookRange * 0.5f, distance);
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
            else if (footLeft == null && t.name == "Foot_L") footLeft = t;
            else if (footRight == null && t.name == "Foot_R") footRight = t;
        }
        if (spine != null) rig = spine.parent;
    }

    /// <summary>Stores the current (standing) pose as the rest pose.</summary>
    public void CaptureRest()
    {
        if (spine == null || head == null || handLeft == null || handRight == null || footLeft == null || footRight == null || rig == null)
        {
            Debug.LogWarning($"{name}: ClimberRestAnimator needs the bones Spine00, Head, Arm_L, Arm_R, Foot_L and Foot_R.", this);
            return;
        }
        spineRestPos = spine.localPosition; spineRestRot = spine.localRotation;
        headRestPos = head.localPosition; headRestRot = head.localRotation;
        leftRestPos = handLeft.localPosition; leftRestRot = handLeft.localRotation;
        rightRestPos = handRight.localPosition; rightRestRot = handRight.localRotation;
        footLeftRestPos = footLeft.localPosition; footRightRestPos = footRight.localPosition;
        rigRestPos = rig.localPosition;
        restCaptured = true;
    }

    /// <summary>
    /// Puts him on the seat in the scene (editor): the whole rig goes down, so the talking animation
    /// (which works around the spine's own rest position) keeps him seated; the feet go back down to the
    /// ground in front of the bench and the hands rest on the knees.
    /// </summary>
    [ContextMenu("Sit on the seat")]
    public void SitDown()
    {
        if (seated) return;
        FindBones();
        CaptureRest();
        if (!restCaptured) return;
        ApplySeat(+1f);
        handLeft.localPosition = new Vector3(-handsOnKnees.x, handsOnKnees.y, handsOnKnees.z);
        handRight.localPosition = handsOnKnees;
        seated = true;
        CaptureRest();
    }

    /// <summary>Back to the standing pose of the model (editor).</summary>
    [ContextMenu("Stand up")]
    public void StandUp()
    {
        if (!seated) return;
        FindBones();
        CaptureRest();
        if (!restCaptured) return;
        ApplySeat(-1f);
        seated = false;
        CaptureRest();
    }

    // direction +1: from standing to seated, -1: back.
    private void ApplySeat(float direction)
    {
        rig.localPosition = rig.localPosition + new Vector3(0f, sitDrop, 0f) * direction;
        footLeft.localPosition = footLeft.localPosition + (feetOffset + new Vector3(-feetSpread, 0f, 0f)) * direction;
        footRight.localPosition = footRight.localPosition + (feetOffset + new Vector3(feetSpread, 0f, 0f)) * direction;
    }

    /// <summary>Puts the bones back in the standing rest pose.</summary>
    public void RestoreRest()
    {
        if (!restCaptured) return;
        rig.localPosition = rigRestPos;
        spine.localPosition = spineRestPos; spine.localRotation = spineRestRot;
        head.localPosition = headRestPos; head.localRotation = headRestRot;
        handLeft.localPosition = leftRestPos; handLeft.localRotation = leftRestRot;
        handRight.localPosition = rightRestPos; handRight.localRotation = rightRestRot;
        footLeft.localPosition = footLeftRestPos; footRight.localPosition = footRightRestPos;
    }

    /// <summary>Poses the character at time t. playerYaw/playerWeight: head turn toward the player.</summary>
    public void Evaluate(float t, float playerYaw, float playerWeight)
    {
        if (!restCaptured) return;

        // Heavy breathing, and a long sigh now and then (shoulders up, then down).
        float breath = Mathf.Sin(t * Mathf.PI * 2f * breathRate);
        float sigh = 0f;
        float s = Mathf.Repeat(t, sighEvery);
        if (s < sighDuration)
        {
            float u = s / sighDuration;
            sigh = u < 0.4f ? Mathf.SmoothStep(0f, 1f, u / 0.4f) : Mathf.SmoothStep(1f, 0f, (u - 0.4f) / 0.6f);
        }

        // Looking up at the summit he missed.
        float up = 0f;
        float m = Mathf.Repeat(t + 4f, summitEvery);
        if (m < summitDuration) up = Mathf.Sin(m / summitDuration * Mathf.PI);
        up = Mathf.Clamp01(up * 1.6f) * (1f - playerWeight);
        float summitYaw = 0f, summitPitch = -22f;
        if (summit != null)
        {
            Vector3 local = transform.InverseTransformDirection(summit.position - head.position);
            summitYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);
            summitPitch = -Mathf.Clamp(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, 0f, 35f);
        }

        // Body: slumped forward, a little straighter when he looks up or at the player.
        float lean = slump * (1f - 0.5f * up - 0.3f * playerWeight) + breath * 1.5f - sigh * 5f;
        spine.localPosition = spineRestPos + new Vector3(0f, breath * breathing + sigh * 0.12f, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(lean, 0f, 0f);

        // Head: hanging down, up to the summit, or turned to the player.
        float idleYaw = (Mathf.PerlinNoise(t * 0.2f, 3.1f) - 0.5f) * 20f;
        float yaw = Mathf.Lerp(Mathf.Lerp(idleYaw, summitYaw, up), playerYaw, playerWeight);
        float pitch = Mathf.Lerp(Mathf.Lerp(headDown, summitPitch, up), 0f, playerWeight) + sigh * 6f;
        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, yaw, Mathf.Sin(t * 0.4f) * 2f);

        // Forearms on the knees, hands hanging, moving with the breath.
        Vector3 bob = new Vector3(0f, breath * breathing * 0.6f + sigh * 0.1f, 0f);
        handLeft.localPosition = new Vector3(-handsOnKnees.x, handsOnKnees.y, handsOnKnees.z) + bob;
        handRight.localPosition = handsOnKnees + bob;
        handLeft.localRotation = leftRestRot * Quaternion.Euler(20f, 0f, 0f);
        handRight.localRotation = rightRestRot * Quaternion.Euler(20f, 0f, 0f);
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}
