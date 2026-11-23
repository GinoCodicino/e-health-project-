using UnityEngine;

/// <summary>
/// Procedural animation for the Malgaro (herdsman) NPC:
/// - Looks around anxiously searching for the lost calf.
/// - Shakes his head in defeat and spreads his arms in dismay/resignation.
/// - Holds a walking stick in his right hand and smoothly rotates his whole body
///   (even 180-250 degrees behind him) to point the stick toward the target location.
/// Automatically compensates for non-unit parent scales.
/// </summary>
public class MalgaroAnimation : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Walking Stick & Pointing Target")]
    [Tooltip("The location/object where the player needs to look for the calf (can be behind him).")]
    [SerializeField] private Transform calfSearchTarget;
    [Tooltip("Check this in Inspector during Play Mode to test the full turn + pointing gesture.")]
    [SerializeField] private bool isPointing = false;
    [Tooltip("How fast he turns his body and raises the stick.")]
    [SerializeField] private float pointTransitionSpeed = 3.0f;
    [Tooltip("Position of the right hand (holding the stick) when pointing forward (spine space).")]
    [SerializeField] private Vector3 pointingHandOffset = new Vector3(0.45f, 1.55f, 1.85f);
    [Tooltip("Rotation angle applied to the right arm/stick when pointing (adjust X to aim the stick tip).")]
    [SerializeField] private Vector3 pointingArmEuler = new Vector3(-55f, 0f, -15f);
    [Tooltip("Slight body angle offset (degrees) so he stands naturally in a 3/4 stance while pointing.")]
    [SerializeField] private float bodyPointOffsetAngle = -20f;

    [Header("Dismay / Arms Spread (spine space)")]
    [Tooltip("Base resting position of the hands in front of the body.")]
    [SerializeField] private Vector3 handsBaseCenter = new Vector3(0f, 1.05f, 1.15f);
    [Tooltip("Base gap between hands when resting.")]
    [SerializeField] private float baseHandsGap = 1.0f;
    [Tooltip("How wide he spreads his arms in dismay/resignation.")]
    [SerializeField] private float dismayArmSpread = 0.85f;
    [Tooltip("How often he makes the dismayed arm spread + head shake gesture (seconds).")]
    [SerializeField] private float dismayEvery = 6.5f;
    [SerializeField] private float dismayDuration = 2.8f;

    [Header("Posture & Defeated Head Shake")]
    [SerializeField] private float leanForward = 10f;
    [SerializeField] private float hunch = 0.12f;
    [SerializeField] private float breathing = 0.035f;
    [Tooltip("Amplitude of the defeated 'no' head shake (degrees).")]
    [SerializeField] private float headShakeAngle = 14f;
    [Tooltip("Chin lowered in defeat (degrees).")]
    [SerializeField] private float defeatedHeadPitch = 10f;

    [Header("Looking Around & Camera Tracking")]
    [Tooltip("Wide scanning angle as he searches the pastures for the calf (degrees).")]
    [SerializeField] private float searchLookAngle = 48f;
    [SerializeField] private float searchSpeed = 0.45f;
    [SerializeField] private float lookRange = 18f;
    [SerializeField] private float maxHeadYaw = 70f;
    [Tooltip("Extra shoulder/spine twist when pointing at sharp angles.")]
    [SerializeField] private float maxSpinePointTwist = 35f;
    [SerializeField] private float lookSpeed = 3.5f;
    [Tooltip("Check this if the model's front faces -Z instead of +Z.")]
    [SerializeField] private bool invertForward = false;
    [Tooltip("Player/Camera to watch when talking. Auto-assigns Camera.main if empty.")]
    [SerializeField] private Transform targetToWatch;

    private Quaternion rootRestRot;
    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;

    private float headYaw;
    private float spineYawOffset;
    private float lookWeight;
    private float pointWeight;
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

    /// <summary>
    /// Call this from your dialogue system when the Malgaro tells the player where to search for the calf.
    /// </summary>
    public void PointAtCalf()
    {
        isPointing = true;
    }

    /// <summary>
    /// Call this to make him lower the stick and turn back to his original orientation.
    /// </summary>
    public void StopPointing()
    {
        isPointing = false;
    }

    /// <summary>
    /// Optional: Call this to point at a specific Transform dynamically.
    /// </summary>
    public void PointAtTarget(Transform newTarget)
    {
        calfSearchTarget = newTarget;
        isPointing = true;
    }

    private void LateUpdate()
    {
        if (targetToWatch == null && Camera.main != null)
        {
            targetToWatch = Camera.main.transform;
        }

        float k = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);
        float pk = 1f - Mathf.Exp(-pointTransitionSpeed * Time.deltaTime);

        // 1. Rotazione dell'intero corpo (Root): ruota SOLO mentre sta indicando.
        // Quando smette di indicare, resta orientato dov'è e abbassa semplicemente il bastone.
        if (isPointing && calfSearchTarget != null)
        {
            Vector3 toCalf = calfSearchTarget.position - transform.position;
            toCalf.y = 0f;
            if (toCalf.sqrMagnitude > 0.001f)
            {
                Vector3 dir = invertForward ? -toCalf.normalized : toCalf.normalized;
                Quaternion desiredRootRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, bodyPointOffsetAngle, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRootRot, pk);
            }
        }

        // 2. Peso della posa di puntamento
        pointWeight = Mathf.Lerp(pointWeight, isPointing ? 1f : 0f, pk);

        // 3. Calcolo angolo residuo per testa, busto e braccio verso il bersaglio attivo
        float targetWeight = 0f;
        float targetYaw = 0f;
        float targetSpineYaw = 0f;

        Transform activeLookTarget = (isPointing && calfSearchTarget != null) ? calfSearchTarget : targetToWatch;

        if (activeLookTarget != null)
        {
            Vector3 toTarget = activeLookTarget.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            float effectiveRange = isPointing ? 500f : lookRange;
            if (distance < effectiveRange && distance > 0.01f)
            {
                Vector3 local = transform.InverseTransformDirection(toTarget / distance);
                if (invertForward) local = -local;

                // SignedAngle restituisce l'angolo esatto da -180 a +180 rispetto al fronte attuale del corpo
                float signedAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

                if (isPointing)
                {
                    // Quando sta ruotando il corpo per puntare, busto e testa compensano lo scarto residuo
                    targetSpineYaw = Mathf.Clamp(signedAngle * 0.5f, -maxSpinePointTwist, maxSpinePointTwist);
                    targetYaw = Mathf.Clamp(signedAngle - targetSpineYaw, -maxHeadYaw, maxHeadYaw);
                    targetWeight = 1f;
                }
                else if (local.z > -0.3f)
                {
                    targetYaw = Mathf.Clamp(signedAngle, -maxHeadYaw, maxHeadYaw);
                    targetSpineYaw = targetYaw * 0.2f;
                    targetWeight = Mathf.InverseLerp(lookRange, lookRange * 0.6f, distance);
                }
            }
        }

        lookWeight = Mathf.Lerp(lookWeight, targetWeight, k);
        headYaw = Mathf.Lerp(headYaw, targetYaw, k);
        spineYawOffset = Mathf.Lerp(spineYawOffset, targetSpineYaw, k);

        Evaluate(Time.time + timeOffset, headYaw, spineYawOffset, lookWeight, pointWeight);
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
        rootRestRot = transform.rotation;
        if (spine == null || head == null || handLeft == null || handRight == null) return;
        spineRestPos = spine.localPosition; spineRestRot = spine.localRotation;
        headRestPos = head.localPosition; headRestRot = head.localRotation;
        leftRestPos = handLeft.localPosition; leftRestRot = handLeft.localRotation;
        rightRestPos = handRight.localPosition; rightRestRot = handRight.localRotation;
        restCaptured = true;
    }

    public void RestoreRest()
    {
        transform.rotation = rootRestRot;
        if (!restCaptured) return;
        spine.localPosition = spineRestPos; spine.localRotation = spineRestRot;
        head.localPosition = headRestPos; head.localRotation = headRestRot;
        handLeft.localPosition = leftRestPos; handLeft.localRotation = leftRestRot;
        handRight.localPosition = rightRestPos; handRight.localRotation = rightRestRot;
    }

    public void Evaluate(float t, float activeHeadYaw, float activeSpineYaw, float activeLookWeight, float currentPointWeight)
    {
        if (!restCaptured) return;

        float parentScale = spine.parent != null ? spine.parent.lossyScale.y : 1f;
        float invScale = parentScale > 0.0001f ? (1f / parentScale) : 1f;

        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.28f);

        // Ciclo di sconforto: allarga le braccia e scuote la testa sconfitto
        float dismayEnvelope = 0f;
        float headShake = 0f;
        float d = Mathf.Repeat(t, dismayEvery);
        if (d < dismayDuration)
        {
            float u = d / dismayDuration;
            dismayEnvelope = Mathf.Sin(u * Mathf.PI);
            headShake = Mathf.Sin(u * Mathf.PI * 4f) * dismayEnvelope * headShakeAngle;
        }
        dismayEnvelope *= (1f - currentPointWeight);
        headShake *= (1f - currentPointWeight);

        // 1. BUSTO
        float sighDrop = dismayEnvelope * 0.08f;
        float torsoY = (-hunch - sighDrop + breath * breathing) * invScale;

        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(
            leanForward + (dismayEnvelope * 4f) - (currentPointWeight * 3f) + breath * 1.2f,
            activeSpineYaw * activeLookWeight,
            Mathf.Sin(t * 0.7f) * 1.5f
        );

        // 2. TESTA
        float scanNoise = (Mathf.PerlinNoise(t * searchSpeed, 4.2f) - 0.5f) * 2f;
        float searchYaw = Mathf.Sin(t * searchSpeed * 1.3f) * (searchLookAngle * 0.65f) + scanNoise * (searchLookAngle * 0.35f);

        float baseYaw = Mathf.Lerp(searchYaw, activeHeadYaw, activeLookWeight);
        float finalYaw = baseYaw + headShake;

        float searchPitch = -4f * (1f - activeLookWeight);
        float finalPitch = Mathf.Lerp(searchPitch, 2f, activeLookWeight) + (dismayEnvelope * defeatedHeadPitch) - (currentPointWeight * 5f);
        float finalRoll = (dismayEnvelope * 6f) + Mathf.Sin(t * 0.8f) * 2f;

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(finalPitch, finalYaw, finalRoll);

        // 3. BRACCIA E BASTONE
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        // Posa Sconsolata
        float currentGap = (baseHandsGap + dismayEnvelope * dismayArmSpread) * handInvScale;
        float dismayLift = (dismayEnvelope * 0.22f + breath * breathing) * handInvScale;
        float dismayBack = (-dismayEnvelope * 0.15f) * handInvScale;

        Vector3 scaledBaseCenter = handsBaseCenter * handInvScale;
        Vector3 leftDismayPos = scaledBaseCenter + new Vector3(-currentGap * 0.5f, dismayLift, dismayBack);
        Vector3 rightDismayPos = scaledBaseCenter + new Vector3(currentGap * 0.5f, dismayLift, dismayBack);

        float spreadAngle = 12f + (dismayEnvelope * 38f);
        float shrugAngle = -dismayEnvelope * 18f;
        Quaternion leftDismayRot = leftRestRot * Quaternion.Euler(shrugAngle, -dismayEnvelope * 20f, -spreadAngle);
        Quaternion rightDismayRot = rightRestRot * Quaternion.Euler(shrugAngle, dismayEnvelope * 20f, spreadAngle);

        // Posa di Puntamento
        Vector3 leftPointPos = scaledBaseCenter + new Vector3(-(baseHandsGap + 0.35f) * 0.5f * handInvScale, 0.15f * handInvScale, 0.1f * handInvScale);
        Quaternion leftPointRot = leftRestRot * Quaternion.Euler(-15f, -15f, -28f);

        Vector3 rightPointPos = pointingHandOffset * handInvScale;
        // Mira con precisione l'angolo residuo tra il busto e il bersaglio
        float armAimYaw = (activeHeadYaw + activeSpineYaw * 0.5f);
        Quaternion rightPointRot = rightRestRot * Quaternion.Euler(pointingArmEuler.x, pointingArmEuler.y + armAimYaw, pointingArmEuler.z);

        // Fusione fluida
        handLeft.localPosition = Vector3.Lerp(leftDismayPos, leftPointPos, currentPointWeight);
        handLeft.localRotation = Quaternion.Slerp(leftDismayRot, leftPointRot, currentPointWeight);

        handRight.localPosition = Vector3.Lerp(rightDismayPos, rightPointPos, currentPointWeight);
        handRight.localRotation = Quaternion.Slerp(rightDismayRot, rightPointRot, currentPointWeight);
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}