using UnityEngine;

/// <summary>
/// Procedural idle animation for an Old Lady (cubic character without Animator):
/// hunched posture, gentle weight shift, expressive subtle arm movements,
/// subtle age tremor, occasional slow nodding, and immediate head/upper-body tracking
/// toward the Main Camera as soon as the player enters the frame.
/// Automatically compensates for non-unit parent scales (e.g., 27.7732).
/// </summary>
public class OldLadyAnimation : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Expressive Arms & Hands (spine space)")]
    [Tooltip("Base resting position of the hands in front of the body.")]
    [SerializeField] private Vector3 handsCenter = new Vector3(0f, 0.95f, 1.25f);
    [Tooltip("Base distance between the two hands.")]
    [SerializeField] private float handsGap = 0.85f;
    [Tooltip("Extra widening of the arms when she notices and looks at the camera.")]
    [SerializeField] private float greetArmSpread = 0.45f;
    [Tooltip("Amplitude of the gentle idle arm/hand motion.")]
    [SerializeField] private float armSwayAmplitude = 0.14f;
    [SerializeField] private float armSwaySpeed = 0.85f;

    [Header("Elderly Posture & Sway")]
    [Tooltip("Pronounced forward hunch (degrees).")]
    [SerializeField] private float leanForward = 16f;
    [Tooltip("Shoulders lowered (spine space).")]
    [SerializeField] private float hunch = 0.16f;
    [Tooltip("Slow side-to-side weight shift (degrees).")]
    [SerializeField] private float weightShiftAngle = 3.0f;
    [SerializeField] private float weightShiftSpeed = 0.4f;
    [Tooltip("Slow breathing amplitude (spine space).")]
    [SerializeField] private float breathing = 0.04f;

    [Header("Subtle Tremor & Nodding")]
    [Tooltip("Subtle hand tremor intensity.")]
    [SerializeField] private float handTremor = 0.015f;
    [Tooltip("Subtle head tremor (degrees).")]
    [SerializeField] private float headTremor = 0.6f;
    [Tooltip("Seconds between slow thoughtful nods.")]
    [SerializeField] private float nodEvery = 6.5f;
    [SerializeField] private float nodDuration = 2.0f;

    [Header("Looking at the Main Camera (Long Range)")]
    [Tooltip("Chin slightly tucked in rest pose (degrees).")]
    [SerializeField] private float headDown = 5f;
    [Tooltip("Slight head tilt (degrees).")]
    [SerializeField] private float headTilt = 6f;
    [Tooltip("Slow curious glances when camera is out of range (degrees).")]
    [SerializeField] private float glanceAngle = 25f;
    [Tooltip("Large distance so she spots and tracks the camera as soon as she enters the frame.")]
    [SerializeField] private float lookRange = 35f;
    [SerializeField] private float maxHeadYaw = 75f;
    [SerializeField] private float maxHeadPitch = 25f;
    [Tooltip("Responsive head turn speed so she locks onto the camera quickly.")]
    [SerializeField] private float lookSpeed = 4.5f;
    [Tooltip("Check this if the model's front faces -Z instead of +Z.")]
    [SerializeField] private bool invertForward = false;
    [Tooltip("Target to watch. If empty, automatically uses Camera.main.")]
    [SerializeField] private Transform targetToWatch;

    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;
    private float headYaw;
    private float headPitchOffset;
    private float lookWeight;
    private float timeOffset;

    private void Awake()
    {
        FindBones();
        CaptureRest();
        timeOffset = Random.Range(0f, 10f);
    }

    private void Start()
    {
        if (targetToWatch == null && Camera.main != null)
        {
            targetToWatch = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (targetToWatch == null && Camera.main != null)
        {
            targetToWatch = Camera.main.transform;
        }

        float targetWeight = 0f;
        float targetYaw = 0f;
        float targetPitch = 0f;

        if (targetToWatch != null)
        {
            // Calcolo direzione dalla testa (o dal busto) verso la Main Camera
            Vector3 origin = head != null ? head.position : transform.position;
            Vector3 toTarget = targetToWatch.position - origin;
            float fullDistance = toTarget.magnitude;

            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            float flatDistance = flatToTarget.magnitude;

            if (fullDistance < lookRange && flatDistance > 0.01f)
            {
                Vector3 localFlat = transform.InverseTransformDirection(flatToTarget / flatDistance);
                if (invertForward) localFlat = -localFlat;

                // Appena il giocatore/camera è davanti (anche molto di lato), lo segue con lo sguardo
                if (localFlat.z > -0.35f)
                {
                    targetYaw = Mathf.Clamp(Mathf.Atan2(localFlat.x, Mathf.Max(0.01f, localFlat.z)) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw);

                    // Calcolo angolo verticale (pitch) per guardare la camera anche su terreni in pendenza
                    float elevationAngle = -Mathf.Atan2(toTarget.y, flatDistance) * Mathf.Rad2Deg;
                    targetPitch = Mathf.Clamp(elevationAngle, -maxHeadPitch, maxHeadPitch);

                    // Rampa rapida: già al 90% del lookRange inizia a guardarti pienamente
                    targetWeight = Mathf.InverseLerp(lookRange, lookRange * 0.75f, fullDistance);
                }
            }
        }

        float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);
        lookWeight = Mathf.Lerp(lookWeight, targetWeight, k);
        headYaw = Mathf.Lerp(headYaw, targetYaw, k);
        headPitchOffset = Mathf.Lerp(headPitchOffset, targetPitch, k);

        Evaluate(Time.time + timeOffset, headYaw, headPitchOffset, lookWeight);
    }

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

    public void CaptureRest()
    {
        if (spine == null || head == null || handLeft == null || handRight == null) return;
        spineRestPos = spine.localPosition; spineRestRot = spine.localRotation;
        headRestPos = head.localPosition; headRestRot = head.localRotation;
        leftRestPos = handLeft.localPosition; leftRestRot = handLeft.localRotation;
        rightRestPos = handRight.localPosition; rightRestRot = handRight.localRotation;
        restCaptured = true;
    }

    public void RestoreRest()
    {
        if (!restCaptured) return;
        spine.localPosition = spineRestPos; spine.localRotation = spineRestRot;
        head.localPosition = headRestPos; head.localRotation = headRestRot;
        handLeft.localPosition = leftRestPos; handLeft.localRotation = leftRestRot;
        handRight.localPosition = rightRestPos; handRight.localRotation = rightRestRot;
    }

    public void Evaluate(float t, float playerYaw, float playerPitch, float playerWeight)
    {
        if (!restCaptured) return;

        // Compensazione automatica della scala del genitore
        float parentScale = spine.parent != null ? spine.parent.lossyScale.y : 1f;
        float invScale = parentScale > 0.0001f ? (1f / parentScale) : 1f;

        // Respiro e dondolio del corpo
        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.25f);
        float sway = Mathf.Sin(t * Mathf.PI * 2f * weightShiftSpeed);

        // Cenni lenti del capo
        float nod = 0f;
        float n = Mathf.Repeat(t, nodEvery);
        if (n < nodDuration)
        {
            float u = n / nodDuration;
            float envelope = Mathf.Sin(u * Mathf.PI);
            nod = Mathf.Sin(u * Mathf.PI * 4f) * envelope * 6.5f;
        }

        // Busto: segue leggermente con le spalle la direzione dello sguardo (25% dello yaw della testa)
        float torsoY = (-hunch + breath * breathing) * invScale;
        float torsoFollowYaw = playerYaw * 0.25f * playerWeight;
        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(
            leanForward + breath * 1.3f - (playerWeight * 3f),
            sway * 2.0f + torsoFollowYaw,
            sway * weightShiftAngle
        );

        // Testa: aggancia lo sguardo della camera sia in orizzontale che in verticale
        float glance = (Mathf.PerlinNoise(t * 0.25f, 3.1f) - 0.5f) * 2f * glanceAngle;
        float yaw = Mathf.Lerp(glance, playerYaw, playerWeight);
        float tremorY = (Mathf.PerlinNoise(t * 4.5f, 1.2f) - 0.5f) * 2f * headTremor;
        float tremorX = (Mathf.PerlinNoise(t * 4.2f, 8.7f) - 0.5f) * 2f * (headTremor * 0.7f);

        float basePitch = headDown + nod + tremorX;
        float targetPitch = playerPitch + (nod * 0.6f) + tremorX;
        float pitch = Mathf.Lerp(basePitch, targetPitch, playerWeight);
        float roll = headTilt * (1f - playerWeight * 0.2f) - sway * (weightShiftAngle * 0.5f);

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, yaw + tremorY, roll);

        // Braccia e mani: movimento sincronizzato (come se fossero congiunte)
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        float armPhase = t * Mathf.PI * 2f * armSwaySpeed;
        float idleOpenClose = (Mathf.Sin(t * Mathf.PI * 2f * 0.18f) + 1f) * 0.5f * 0.15f;

        float dynamicGap = (handsGap + idleOpenClose + (playerWeight * greetArmSpread)) * handInvScale;
        float greetLift = playerWeight * 0.10f * handInvScale;

        // Un unico movimento condiviso da entrambe le braccia per farle restare unite
        Vector3 sharedMotion = new Vector3(
            (sway * 0.05f) * handInvScale,
            (Mathf.Sin(armPhase) * armSwayAmplitude + breath * breathing) * handInvScale + greetLift,
            (Mathf.Cos(armPhase) * (armSwayAmplitude * 0.6f)) * handInvScale
        );

        Vector3 tremorOffset = new Vector3(
            (Mathf.PerlinNoise(t * 5.5f, 2.4f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t * 5.2f, 6.8f) - 0.5f) * 2f,
            0f
        ) * (handTremor * handInvScale);

        Vector3 scaledHandsCenter = handsCenter * handInvScale;

        // Entrambe le mani usano lo stesso sharedMotion e lo stesso tremorOffset
        handLeft.localPosition = scaledHandsCenter + new Vector3(-dynamicGap * 0.5f, 0f, 0f) + sharedMotion + tremorOffset;
        handRight.localPosition = scaledHandsCenter + new Vector3(dynamicGap * 0.5f, 0f, 0f) + sharedMotion + tremorOffset;

        // Rotazioni simmetriche e sincronizzate
        float armSwingAngle = Mathf.Sin(armPhase) * 5f;
        float armSpreadAngle = 10f + (playerWeight * 10f) + (idleOpenClose * 15f);
        handLeft.localRotation = leftRestRot * Quaternion.Euler(armSwingAngle, 12f, -armSpreadAngle);
        handRight.localRotation = rightRestRot * Quaternion.Euler(armSwingAngle, -12f, armSpreadAngle);
    }
    private void OnDisable()
    {
        RestoreRest();
    }
}