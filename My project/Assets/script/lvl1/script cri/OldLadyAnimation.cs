using UnityEngine;

/// <summary>
/// Procedural animation for the Elderly Lady NPC:
/// 1. Idle (Tired & composed): Clasps her hands together in front of her with slow breathing and subtle tremor.
/// 2. Talking: Opens/spreads her arms slowly while speaking and looking at the player.
/// 3. Pointing at River: Turns slightly and points her right arm toward the river target.
/// Automatically compensates for non-unit parent scales.
/// </summary>
public class OldLadyAnimation : MonoBehaviour
{
    public enum StatoAnziana
    {
        RiposoManiGiunte,
        ParlandoBracciaAperte,
        IndicandoFiume
    }

    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Stato Attuale (Testabile in Play Mode)")]
    [SerializeField] private StatoAnziana statoAttuale = StatoAnziana.RiposoManiGiunte;
    [SerializeField] private float transitionSpeed = 2.5f;
    [Tooltip("Check this if the model's front faces -Z instead of +Z.")]
    [SerializeField] private bool invertForward = false;
    [SerializeField] private float modelFacingOffset = 0f;

    [Header("1. Mani Giunte & Stanchezza (Riposo)")]
    [SerializeField] private Vector3 claspedHandsCenter = new Vector3(0f, 0.85f, 1.25f);
    [SerializeField] private float claspedHandsGap = 0.35f;
    [SerializeField] private float tiredLeanForward = 12f;
    [SerializeField] private float tiredHeadPitch = 10f;
    [SerializeField] private float hunch = 0.14f;
    [SerializeField] private float breathing = 0.04f;
    [SerializeField] private float handTremor = 0.015f;

    [Header("2. Braccia Aperte (Parlando)")]
    [SerializeField] private Vector3 talkHandsCenter = new Vector3(0f, 1.05f, 1.3f);
    [SerializeField] private float talkHandsGap = 1.25f;
    [SerializeField] private float armSwayAmplitude = 0.10f;
    [SerializeField] private float armSwaySpeed = 0.85f;

    [Header("3. Indicare il Fiume")]
    [Tooltip("Trascina qui un Empty GameObject posizionato nella direzione del fiume.")]
    [SerializeField] private Transform riverTarget;
    [SerializeField] private Vector3 pointingHandOffset = new Vector3(0.45f, 1.45f, 1.8f);
    [SerializeField] private Vector3 pointingArmEuler = new Vector3(-50f, 0f, -15f);
    [SerializeField] private float bodyPointOffsetAngle = -25f;

    [Header("Sguardo e Tracciamento Player")]
    [SerializeField] private float lookRange = 14f;
    [SerializeField] private float maxHeadYaw = 60f;
    [SerializeField] private float lookSpeed = 2.8f;
    [SerializeField] private Transform targetToWatch;

    private Quaternion restRootRot;
    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;

    private float idleWeight = 1f;
    private float talkWeight = 0f;
    private float pointWeight = 0f;

    private float headYaw;
    private float spineYawOffset;
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

    // =========================================================================
    // METODI PUBBLICI DA CHIAMARE DALLO SCRIPT DEL DIALOGO DELL'ANZIANA
    // =========================================================================

    /// <summary>Chiama questo quando inizia a parlare (allarga le braccia).</summary>
    public void StartTalking()
    {
        statoAttuale = StatoAnziana.ParlandoBracciaAperte;
    }

    /// <summary>Chiama questo nella frase in cui parla del fiume (indica verso il fiume).</summary>
    public void PointAtRiver()
    {
        statoAttuale = StatoAnziana.IndicandoFiume;
    }

    /// <summary>Chiama questo quando finisce il dialogo (torna alle mani giunte).</summary>
    public void StopTalking()
    {
        statoAttuale = StatoAnziana.RiposoManiGiunte;
    }

    // =========================================================================

    private void LateUpdate()
    {
        if (targetToWatch == null && Camera.main != null)
        {
            targetToWatch = Camera.main.transform;
        }

        float k = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        float lk = 1f - Mathf.Exp(-lookSpeed * Time.deltaTime);

        // Pesi fluidi tra i 3 stati
        idleWeight = Mathf.Lerp(idleWeight, statoAttuale == StatoAnziana.RiposoManiGiunte ? 1f : 0f, k);
        talkWeight = Mathf.Lerp(talkWeight, statoAttuale == StatoAnziana.ParlandoBracciaAperte ? 1f : 0f, k);
        pointWeight = Mathf.Lerp(pointWeight, statoAttuale == StatoAnziana.IndicandoFiume ? 1f : 0f, k);

        // 1. Rotazione del corpo in base allo stato
        if (statoAttuale == StatoAnziana.IndicandoFiume && riverTarget != null)
        {
            Vector3 toRiver = riverTarget.position - transform.position;
            toRiver.y = 0f;
            if (toRiver.sqrMagnitude > 0.001f)
            {
                Vector3 dir = invertForward ? -toRiver.normalized : toRiver.normalized;
                Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, bodyPointOffsetAngle + modelFacingOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, k);
            }
        }
        else if (statoAttuale == StatoAnziana.ParlandoBracciaAperte && targetToWatch != null)
        {
            Vector3 toPlayer = targetToWatch.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Vector3 dir = invertForward ? -toPlayer.normalized : toPlayer.normalized;
                Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, modelFacingOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, k);
            }
        }
        else if (statoAttuale == StatoAnziana.RiposoManiGiunte)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, restRootRot, k);
        }

        // 2. Calcolo direzione sguardo
        float targetWeight = 0f;
        float targetHeadYaw = 0f;
        float targetSpineYaw = 0f;

        Transform activeTarget = (statoAttuale == StatoAnziana.IndicandoFiume && riverTarget != null)
            ? riverTarget
            : targetToWatch;

        if (activeTarget != null)
        {
            Vector3 toTarget = activeTarget.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            float effectiveRange = (statoAttuale == StatoAnziana.IndicandoFiume) ? 400f : lookRange;
            if (distance < effectiveRange && distance > 0.01f)
            {
                Vector3 local = transform.InverseTransformDirection(toTarget / distance);
                if (invertForward) local = -local;

                if (local.z > -0.3f || statoAttuale == StatoAnziana.IndicandoFiume)
                {
                    float signedAngle = Mathf.Atan2(local.x, Mathf.Max(0.01f, local.z)) * Mathf.Rad2Deg;
                    if (statoAttuale == StatoAnziana.IndicandoFiume)
                    {
                        targetSpineYaw = Mathf.Clamp(signedAngle * 0.4f, -30f, 30f);
                        targetHeadYaw = Mathf.Clamp(signedAngle - targetSpineYaw, -maxHeadYaw, maxHeadYaw);
                        targetWeight = 1f;
                    }
                    else
                    {
                        targetHeadYaw = Mathf.Clamp(signedAngle, -maxHeadYaw, maxHeadYaw);
                        targetSpineYaw = targetHeadYaw * 0.15f;
                        targetWeight = Mathf.InverseLerp(lookRange, lookRange * 0.6f, distance);
                    }
                }
            }
        }

        lookWeight = Mathf.Lerp(lookWeight, targetWeight, lk);
        headYaw = Mathf.Lerp(headYaw, targetHeadYaw, lk);
        spineYawOffset = Mathf.Lerp(spineYawOffset, targetSpineYaw, lk);

        Evaluate(Time.time + timeOffset);
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
        restRootRot = transform.rotation;
        if (spine == null || head == null || handLeft == null || handRight == null) return;
        spineRestPos = spine.localPosition; spineRestRot = spine.localRotation;
        headRestPos = head.localPosition; headRestRot = head.localRotation;
        leftRestPos = handLeft.localPosition; leftRestRot = handLeft.localRotation;
        rightRestPos = handRight.localPosition; rightRestRot = handRight.localRotation;
        restCaptured = true;
    }

    public void RestoreRest()
    {
        transform.rotation = restRootRot;
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

        // Respiro lento e un po' affaticato
        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.22f);
        float sway = Mathf.Sin(t * Mathf.PI * 2f * 0.18f);

        // --- 1. BUSTO ---
        // Più china in riposo per la stanchezza, leggermente più dritta quando racconta o indica
        float lean = Mathf.Lerp(tiredLeanForward - 4f, tiredLeanForward, idleWeight) + (breath * 1.5f);
        float torsoY = (-hunch + breath * breathing) * invScale;

        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(
            lean,
            spineYawOffset * lookWeight,
            sway * 1.5f
        );

        // --- 2. TESTA ---
        // In riposo guarda un po' verso il basso (stanca), quando parla/indica alza lo sguardo e annuisce piano
        float talkNod = Mathf.Sin(t * 2.4f) * 3f * talkWeight;
        float idlePitch = Mathf.Lerp(tiredHeadPitch, 2f, lookWeight);
        float activePitch = (idlePitch * idleWeight) + ((2f + talkNod) * talkWeight) + (-3f * pointWeight);

        float idleScan = sway * 8f * (1f - lookWeight) * idleWeight;
        float finalYaw = (headYaw * lookWeight) + idleScan;
        float finalRoll = (sway * 2.5f) + (Mathf.Sin(t * 1.1f) * 3f * talkWeight);

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(activePitch, finalYaw, finalRoll);

        // --- 3. BRACCIA E MANI ---
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        // Tremolio leggero condiviso
        Vector3 tremorOffset = new Vector3(
            (Mathf.PerlinNoise(t * 5.0f, 2.4f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t * 4.8f, 6.8f) - 0.5f) * 2f,
            0f
        ) * (handTremor * handInvScale);

        // A) Posa Riposo: Mani giunte sincronizzate davanti al corpo
        float breathLift = (breath * breathing * 0.8f) * handInvScale;
        Vector3 sharedIdleMotion = new Vector3(sway * 0.04f * handInvScale, breathLift, 0f);
        Vector3 scaledClaspedCenter = claspedHandsCenter * handInvScale;
        float halfClaspedGap = claspedHandsGap * 0.5f * handInvScale;

        Vector3 leftIdlePos = scaledClaspedCenter + new Vector3(-halfClaspedGap, 0f, 0f) + sharedIdleMotion + tremorOffset;
        Vector3 rightIdlePos = scaledClaspedCenter + new Vector3(halfClaspedGap, 0f, 0f) + sharedIdleMotion + tremorOffset;

        Quaternion leftIdleRot = leftRestRot * Quaternion.Euler(4f, 14f, -10f);
        Quaternion rightIdleRot = rightRestRot * Quaternion.Euler(4f, -14f, 10f);

        // B) Posa Parlando: Braccia allargate con gesti lenti e morbidi
        float armPhase = t * Mathf.PI * 2f * armSwaySpeed;
        Vector3 scaledTalkCenter = talkHandsCenter * handInvScale;
        float halfTalkGap = talkHandsGap * 0.5f * handInvScale;

        Vector3 leftTalkPos = scaledTalkCenter + new Vector3(
            -halfTalkGap,
            (Mathf.Sin(armPhase) * armSwayAmplitude + breath * breathing) * handInvScale,
            (Mathf.Cos(armPhase) * 0.06f) * handInvScale
        ) + tremorOffset;

        Vector3 rightTalkPos = scaledTalkCenter + new Vector3(
            halfTalkGap,
            (Mathf.Cos(armPhase * 0.9f) * armSwayAmplitude + breath * breathing) * handInvScale,
            (Mathf.Sin(armPhase * 0.9f) * 0.06f) * handInvScale
        ) + tremorOffset;

        Quaternion leftTalkRot = leftRestRot * Quaternion.Euler(-15f + Mathf.Sin(armPhase) * 8f, -18f, -32f);
        Quaternion rightTalkRot = rightRestRot * Quaternion.Euler(-15f + Mathf.Cos(armPhase * 0.9f) * 8f, 18f, 32f);

        // C) Posa Indica il Fiume: Braccio destro alzato a indicare, sinistro aperto
        Vector3 leftPointPos = scaledTalkCenter + new Vector3(-halfTalkGap * 0.85f, 0.05f * handInvScale, 0f) + tremorOffset;
        Quaternion leftPointRot = leftRestRot * Quaternion.Euler(-10f, -10f, -24f);

        Vector3 rightPointPos = (pointingHandOffset * handInvScale) + tremorOffset;
        float aimYaw = headYaw + spineYawOffset * 0.5f;
        Quaternion rightPointRot = rightRestRot * Quaternion.Euler(pointingArmEuler.x, pointingArmEuler.y + aimYaw, pointingArmEuler.z);

        // Miscelazione fluida tra i 3 stati
        handLeft.localPosition = (leftIdlePos * idleWeight) + (leftTalkPos * talkWeight) + (leftPointPos * pointWeight);
        handRight.localPosition = (rightIdlePos * idleWeight) + (rightTalkPos * talkWeight) + (rightPointPos * pointWeight);

        float talkRatio = talkWeight / Mathf.Max(0.001f, idleWeight + talkWeight);
        Quaternion blendedLeftRot = Quaternion.Slerp(leftIdleRot, leftTalkRot, talkRatio);
        blendedLeftRot = Quaternion.Slerp(blendedLeftRot, leftPointRot, pointWeight);

        Quaternion blendedRightRot = Quaternion.Slerp(rightIdleRot, rightTalkRot, talkRatio);
        blendedRightRot = Quaternion.Slerp(blendedRightRot, rightPointRot, pointWeight);

        handLeft.localRotation = blendedLeftRot;
        handRight.localRotation = blendedRightRot;
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}