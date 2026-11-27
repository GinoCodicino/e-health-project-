using UnityEngine;

/// <summary>
/// NPC 1 (Narratore/Esplicativo): Gesticola con entrambe le mani in modo asincrono mentre racconta,
/// poi si ferma ad ascoltare gli altri scuotendo la testa rattristato.
/// </summary>
public class AlluvioneSpeakerA : MonoBehaviour
{
    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Interlocutori (opzionali, per girare lo sguardo tra loro)")]
    [SerializeField] private Transform interlocutore1;
    [SerializeField] private Transform interlocutore2;
    [SerializeField] private bool invertForward = false;

    [Header("Ciclo Parlata Asincrono")]
    [Tooltip("Durata della fase in cui parla e gesticola.")]
    [SerializeField] private float speakDuration = 4.2f;
    [Tooltip("Durata della fase in cui ascolta e scuote la testa.")]
    [SerializeField] private float listenDuration = 5.5f;
    [Tooltip("Sfasamento iniziale (secondi) per non sincronizzarsi con gli altri NPC.")]
    [SerializeField] private float startPhaseOffset = 0.0f;

    [Header("Gesti Braccia (Parlando)")]
    [SerializeField] private Vector3 talkHandsCenter = new Vector3(0f, 1.15f, 1.35f);
    [SerializeField] private float talkHandsGap = 1.15f;
    [SerializeField] private float gestureAmplitude = 0.22f;
    [SerializeField] private float gestureSpeed = 2.1f;

    [Header("Postura & Tristezza")]
    [SerializeField] private float sadLeanForward = 9f;
    [SerializeField] private float sadHeadPitch = 11f;
    [SerializeField] private float headShakeAngle = 14f;
    [SerializeField] private float hunch = 0.12f;
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

        // Peso morbido tra parlare (1) e ascoltare (0)
        float cycle = speakDuration + listenDuration;
        float inCycle = Mathf.Repeat(t, cycle);
        float speakWeight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inCycle / 0.5f))
                          * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((speakDuration - inCycle) / 0.5f));

        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.25f);

        // --- 1. BUSTO ---
        float lean = sadLeanForward + (speakWeight * 2f) + (breath * 1.3f);
        float torsoY = (-hunch + breath * breathing) * invScale;
        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(lean, Mathf.Sin(t * 0.7f) * 2.5f * speakWeight, 0f);

        // --- 2. TESTA (Sguardo tra interlocutori + scuotimento triste) ---
        float targetYaw = CalcolaYawInterlocutori(t);
        currentLookYaw = Mathf.Lerp(currentLookYaw, targetYaw, Time.deltaTime * 3f);

        // Scuote la testa lentamente quando ascolta o alla fine delle frasi
        float shakeWave = Mathf.Sin(t * 3.2f) * Mathf.Clamp01(Mathf.PerlinNoise(t * 0.4f, 1.3f) * 1.4f - 0.3f);
        float sadShake = shakeWave * headShakeAngle * (1f - speakWeight * 0.55f);

        float pitch = sadHeadPitch - (speakWeight * 4f) + (Mathf.Sin(t * 2.8f) * 2.5f * speakWeight);
        float roll = Mathf.Sin(t * 1.1f) * 4f;

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, currentLookYaw + sadShake, roll);

        // --- 3. BRACCIA (Asincrone quando parla, rilassate in basso quando ascolta) ---
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        Vector3 scaledCenter = talkHandsCenter * handInvScale;
        float halfGap = talkHandsGap * 0.5f * handInvScale;

        // Posa riposo/ascolto (braccia abbassate lungo i fianchi)
        Vector3 leftIdle = new Vector3(-halfGap * 1.05f, 0.45f * handInvScale, 0.35f * handInvScale);
        Vector3 rightIdle = new Vector3(halfGap * 1.05f, 0.45f * handInvScale, 0.35f * handInvScale);

        // Posa parlata asincrona (frequenze diverse tra mano destra e sinistra)
        float pL = t * Mathf.PI * 2f * gestureSpeed;
        float pR = t * Mathf.PI * 2f * (gestureSpeed * 0.83f) + 1.4f;

        Vector3 leftTalk = scaledCenter + new Vector3(
            -halfGap - Mathf.Abs(Mathf.Sin(pL * 0.5f)) * 0.14f * handInvScale,
            (Mathf.Sin(pL) * gestureAmplitude + breath * breathing) * handInvScale,
            (Mathf.Cos(pL) * 0.10f) * handInvScale
        );
        Vector3 rightTalk = scaledCenter + new Vector3(
            halfGap + Mathf.Abs(Mathf.Cos(pR * 0.5f)) * 0.14f * handInvScale,
            (Mathf.Sin(pR) * gestureAmplitude + breath * breathing) * handInvScale,
            (Mathf.Cos(pR) * 0.10f) * handInvScale
        );

        handLeft.localPosition = Vector3.Lerp(leftIdle, leftTalk, speakWeight);
        handRight.localPosition = Vector3.Lerp(rightIdle, rightTalk, speakWeight);

        Quaternion leftIdleRot = leftRestRot * Quaternion.Euler(8f, 0f, -6f);
        Quaternion rightIdleRot = rightRestRot * Quaternion.Euler(8f, 0f, 6f);

        Quaternion leftTalkRot = leftRestRot * Quaternion.Euler(-24f + Mathf.Sin(pL) * 14f, -15f, -24f);
        Quaternion rightTalkRot = rightRestRot * Quaternion.Euler(-24f + Mathf.Sin(pR) * 14f, 15f, 24f);

        handLeft.localRotation = Quaternion.Slerp(leftIdleRot, leftTalkRot, speakWeight);
        handRight.localRotation = Quaternion.Slerp(rightIdleRot, rightTalkRot, speakWeight);
    }

    private float CalcolaYawInterlocutori(float t)
    {
        Transform chosen = null;
        if (interlocutore1 != null && interlocutore2 != null)
            chosen = Mathf.Repeat(t, 7f) < 3.5f ? interlocutore1 : interlocutore2;
        else if (interlocutore1 != null) chosen = interlocutore1;
        else if (interlocutore2 != null) chosen = interlocutore2;

        if (chosen == null) return Mathf.Sin(t * 0.5f) * 18f;

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