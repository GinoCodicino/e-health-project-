using UnityEngine;

/// <summary>
/// NPC 2 (Costernato/Incredulo): Tiene una mano al mento/petto mentre ascolta scuotendo la testa,
/// poi allarga le braccia in segno di sconforto quando interviene nella discussione.
/// </summary>
public class AlluvioneSpeakerB : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Interlocutori (opzionali)")]
    [SerializeField] private Transform interlocutore1;
    [SerializeField] private Transform interlocutore2;
    [SerializeField] private bool invertForward = false;

    [Header("Ciclo Parlata Asincrono (sfalsato rispetto a Speaker A)")]
    [SerializeField] private float speakDuration = 3.4f;
    [SerializeField] private float listenDuration = 6.3f;
    [SerializeField] private float startPhaseOffset = 4.3f;

    [Header("Gesti (Mano al mento / Braccia allargate)")]
    [SerializeField] private Vector3 chinHandOffset = new Vector3(0.22f, 1.55f, 1.15f);
    [SerializeField] private Vector3 openArmsCenter = new Vector3(0f, 1.15f, 1.3f);
    [SerializeField] private float openArmsGap = 1.55f;
    [SerializeField] private float gestureAmplitude = 0.16f;

    [Header("Postura & Tristezza")]
    [SerializeField] private float sadLeanForward = 11f;
    [SerializeField] private float sadHeadPitch = 13f;
    [SerializeField] private float headShakeAngle = 17f;
    [SerializeField] private float hunch = 0.14f;
    [SerializeField] private float breathing = 0.035f;

    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;
    private float currentLookYaw;

    private void Awake()
    {
        FindBones();
        CaptureRest();
    }

    private void LateUpdate()
    {
        Evaluate(Time.time + startPhaseOffset);
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

    private void Evaluate(float t)
    {
        if (!restCaptured) return;

        float parentScale = spine.parent != null ? spine.parent.lossyScale.y : 1f;
        float invScale = parentScale > 0.0001f ? (1f / parentScale) : 1f;

        float cycle = speakDuration + listenDuration;
        float inCycle = Mathf.Repeat(t, cycle);
        float speakWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inCycle / 0.45f))
                          * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((speakDuration - inCycle) / 0.45f));

        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.23f);

        // --- 1. BUSTO ---
        float lean = Mathf.Lerp(sadLeanForward, sadLeanForward - 3f, speakWeight) + (breath * 1.4f);
        float torsoY = (-hunch + breath * breathing) * invScale;
        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(lean, Mathf.Cos(t * 0.6f) * 2f, Mathf.Sin(t * 0.5f) * 1.5f);

        // --- 2. TESTA ---
        float targetYaw = CalcolaYawInterlocutori(t);
        currentLookYaw = Mathf.Lerp(currentLookYaw, targetYaw, Time.deltaTime * 2.8f);

        // Scuote la testa con più frequenza mentre ascolta le brutte notizie
        float shakePulse = Mathf.Sin(t * 3.8f) * Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(t * 0.5f, 4.2f));
        float sadShake = shakePulse * headShakeAngle;

        float pitch = Mathf.Lerp(sadHeadPitch, 4f, speakWeight) + (Mathf.Sin(t * 2.2f) * 2f * speakWeight);
        float roll = -5f + Mathf.Sin(t * 0.9f) * 3.5f;

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, currentLookYaw + sadShake, roll);

        // --- 3. BRACCIA ---
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        // Posa ascolto: mano destra vicino al mento/viso, mano sinistra conserta al petto
        Vector3 leftListen = new Vector3(-0.35f, 0.95f, 1.15f) * handInvScale;
        Vector3 rightListen = (chinHandOffset + new Vector3(0f, breath * 0.02f, 0f)) * handInvScale;

        // Posa parlata: allarga le braccia in modo asincrono per lo sconforto
        float pL = t * 5.2f;
        float pR = t * 4.3f + 1.1f;
        Vector3 scaledOpen = openArmsCenter * handInvScale;
        float halfOpenGap = openArmsGap * 0.5f * handInvScale;

        Vector3 leftSpeak = scaledOpen + new Vector3(
            -halfOpenGap - Mathf.Abs(Mathf.Sin(pL * 0.5f)) * 0.18f * handInvScale,
            (Mathf.Sin(pL) * gestureAmplitude) * handInvScale,
            (Mathf.Cos(pL) * 0.09f) * handInvScale
        );
        Vector3 rightSpeak = scaledOpen + new Vector3(
            halfOpenGap + Mathf.Abs(Mathf.Cos(pR * 0.5f)) * 0.18f * handInvScale,
            (Mathf.Cos(pR) * gestureAmplitude) * handInvScale,
            (Mathf.Sin(pR) * 0.09f) * handInvScale
        );

        handLeft.localPosition = Vector3.Lerp(leftListen, leftSpeak, speakWeight);
        handRight.localPosition = Vector3.Lerp(rightListen, rightSpeak, speakWeight);

        Quaternion leftListenRot = leftRestRot * Quaternion.Euler(-15f, 25f, -15f);
        Quaternion rightListenRot = rightRestRot * Quaternion.Euler(-55f, -20f, 18f);

        Quaternion leftSpeakRot = leftRestRot * Quaternion.Euler(-20f + Mathf.Sin(pL) * 12f, -25f, -38f);
        Quaternion rightSpeakRot = rightRestRot * Quaternion.Euler(-20f + Mathf.Cos(pR) * 12f, 25f, 38f);

        handLeft.localRotation = Quaternion.Slerp(leftListenRot, leftSpeakRot, speakWeight);
        handRight.localRotation = Quaternion.Slerp(rightListenRot, rightSpeakRot, speakWeight);
    }

    private float CalcolaYawInterlocutori(float t)
    {
        Transform chosen = null;
        if (interlocutore1 != null && interlocutore2 != null)
            chosen = Mathf.Repeat(t + 2.1f, 6.4f) < 3.2f ? interlocutore1 : interlocutore2;
        else if (interlocutore1 != null) chosen = interlocutore1;
        else if (interlocutore2 != null) chosen = interlocutore2;

        if (chosen == null) return Mathf.Cos(t * 0.45f) * 20f;

        Vector3 toOther = chosen.position - transform.position;
        toOther.y = 0f;
        if (toOther.sqrMagnitude < 0.001f) return 0f;

        Vector3 local = transform.InverseTransformDirection(toOther.normalized);
        if (invertForward) local = -local;
        return Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.01f, local.z)) * Mathf.Rad2Deg, -55f, 55f);
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}