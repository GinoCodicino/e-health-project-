using UnityEngine;

/// <summary>
/// NPC 3 (Rassegnato): Sta con le braccia conserte scuotendo la testa, poi scioglie le braccia
/// e indica con un gesto verso nord dove è avvenuta l'alluvione.
/// </summary>
public class AlluvioneSpeakerC : MonoBehaviour
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

    [Header("Ciclo Parlata Asincrono (sfalsato rispetto a A e B)")]
    [SerializeField] private float speakDuration = 3.1f;
    [SerializeField] private float listenDuration = 6.6f;
    [SerializeField] private float startPhaseOffset = 7.7f;

    [Header("Gesti (Braccia conserte -> Indica verso nord)")]
    [SerializeField] private Vector3 foldedArmsCenter = new Vector3(0f, 1.0f, 1.15f);
    [SerializeField] private Vector3 pointNorthHandPos = new Vector3(0.45f, 1.45f, 1.75f);
    [SerializeField] private Vector3 pointNorthArmEuler = new Vector3(-48f, 20f, -15f);
    [SerializeField] private float gestureAmplitude = 0.14f;

    [Header("Postura & Tristezza")]
    [SerializeField] private float sadLeanForward = 10f;
    [SerializeField] private float sadHeadPitch = 14f;
    [SerializeField] private float headShakeAngle = 15f;
    [SerializeField] private float hunch = 0.13f;
    [SerializeField] private float breathing = 0.032f;

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

        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.21f);

        // --- 1. BUSTO ---
        float lean = sadLeanForward + (breath * 1.2f);
        float torsoY = (-hunch + breath * breathing) * invScale;
        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(lean, speakWeight * 8f, 0f);

        // --- 2. TESTA ---
        float targetYaw = CalcolaYawInterlocutori(t);
        currentLookYaw = Mathf.Lerp(currentLookYaw, targetYaw, Time.deltaTime * 2.5f);

        float shakeWave = Mathf.Sin(t * 2.9f) * Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(t * 0.38f, 8.5f));
        float sadShake = shakeWave * headShakeAngle;

        float pitch = Mathf.Lerp(sadHeadPitch, 2f, speakWeight);
        float roll = 4f + Mathf.Cos(t * 0.8f) * 3f;

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(pitch, currentLookYaw + sadShake, roll);

        // --- 3. BRACCIA ---
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        // Posa ascolto: braccia conserte davanti al busto
        Vector3 scaledFolded = foldedArmsCenter * handInvScale;
        Vector3 leftFolded = scaledFolded + new Vector3(-0.22f, 0.04f + breath * 0.02f, 0.05f) * handInvScale;
        Vector3 rightFolded = scaledFolded + new Vector3(0.22f, -0.02f + breath * 0.02f, 0f) * handInvScale;

        // Posa parlata: mano destra indica verso nord, mano sinistra gesticola bassa
        float pL = t * 4.6f;
        Vector3 leftSpeak = new Vector3(
            -0.55f * handInvScale,
            (1.05f + Mathf.Sin(pL) * gestureAmplitude) * handInvScale,
            (1.25f + Mathf.Cos(pL) * 0.08f) * handInvScale
        );
        Vector3 rightSpeak = (pointNorthHandPos + new Vector3(0f, Mathf.Sin(t * 3.2f) * 0.05f, 0f)) * handInvScale;

        handLeft.localPosition = Vector3.Lerp(leftFolded, leftSpeak, speakWeight);
        handRight.localPosition = Vector3.Lerp(rightFolded, rightSpeak, speakWeight);

        Quaternion leftFoldedRot = leftRestRot * Quaternion.Euler(-12f, 35f, -15f);
        Quaternion rightFoldedRot = rightRestRot * Quaternion.Euler(-12f, -35f, 15f);

        Quaternion leftSpeakRot = leftRestRot * Quaternion.Euler(-22f + Mathf.Sin(pL) * 12f, -15f, -25f);
        Quaternion rightSpeakRot = rightRestRot * Quaternion.Euler(pointNorthArmEuler.x, pointNorthArmEuler.y, pointNorthArmEuler.z);

        handLeft.localRotation = Quaternion.Slerp(leftFoldedRot, leftSpeakRot, speakWeight);
        handRight.localRotation = Quaternion.Slerp(rightFoldedRot, rightSpeakRot, speakWeight);
    }

    private float CalcolaYawInterlocutori(float t)
    {
        Transform chosen = null;
        if (interlocutore1 != null && interlocutore2 != null)
            chosen = Mathf.Repeat(t + 1.2f, 8f) < 4f ? interlocutore1 : interlocutore2;
        else if (interlocutore1 != null) chosen = interlocutore1;
        else if (interlocutore2 != null) chosen = interlocutore2;

        if (chosen == null) return Mathf.Sin(t * 0.4f) * 16f;

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