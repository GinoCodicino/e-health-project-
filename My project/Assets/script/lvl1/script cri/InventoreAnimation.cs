using UnityEngine;

/// <summary>
/// Procedural animation for the Inventor NPC (cubic character without Animator):
/// 1. Working (Idle): Faces his workbench/log and saws wood rhythmically while occasionally glancing around.
/// 2. Talking: Turns toward the player and gestures enthusiastically while explaining.
/// 3. Pointing: Turns toward the basket target and points at it with his right arm.
/// Automatically compensates for non-unit parent scales.
/// </summary>
public class InventoreAnimation : MonoBehaviour
{
    public enum StatoInventore
    {
        Lavorando,
        ParlandoConPlayer,
        IndicandoCesta
    }

    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Stato Attuale (Testabile in Play Mode)")]
    [SerializeField] private StatoInventore statoAttuale = StatoInventore.Lavorando;
    [SerializeField] private float transitionSpeed = 3.5f;
    [Tooltip("Check this if the model's front faces -Z instead of +Z.")]
    [SerializeField] private bool invertForward = false;
    [Tooltip("Extra Y rotation offset if needed when facing the player.")]
    [SerializeField] private float modelFacingOffset = 0f;

    [Header("1. Animazione Sega (Lavorando)")]
    [Tooltip("Position of the right hand holding the saw (spine space).")]
    [SerializeField] private Vector3 sawHandCenter = new Vector3(0.45f, 1.15f, 1.35f);
    [Tooltip("Position of the left hand holding the log steady (spine space).")]
    [SerializeField] private Vector3 holdLogHandPos = new Vector3(-0.45f, 1.05f, 1.25f);
    [Tooltip("Forward/backward stroke length of the saw.")]
    [SerializeField] private float sawStrokeLength = 0.45f;
    [Tooltip("Up/down arc of the sawing motion.")]
    [SerializeField] private float sawVerticalArc = 0.12f;
    [Tooltip("Sawing strokes per second.")]
    [SerializeField] private float sawSpeed = 2.4f;
    [Tooltip("Euler rotation of the right hand while gripping the saw.")]
    [SerializeField] private Vector3 sawGripEuler = new Vector3(-35f, 0f, -10f);

    [Header("2. Gesticolare (Parlando con il Player)")]
    [SerializeField] private Vector3 talkHandsCenter = new Vector3(0f, 1.15f, 1.25f);
    [SerializeField] private float talkHandsGap = 1.1f;
    [SerializeField] private float gestureAmplitude = 0.18f;
    [SerializeField] private float gestureSpeed = 2.2f;

    [Header("3. Indicare la Cesta")]
    [Tooltip("Trascina qui l'oggetto (o un Empty) dove si trova la cesta per i tronchi.")]
    [SerializeField] private Transform basketTarget;
    [SerializeField] private Vector3 pointingHandOffset = new Vector3(0.45f, 1.5f, 1.85f);
    [SerializeField] private Vector3 pointingArmEuler = new Vector3(-55f, 0f, -15f);
    [SerializeField] private float bodyPointOffsetAngle = -25f;

    [Header("Postura & Sguardo")]
    [SerializeField] private float workLeanForward = 16f;
    [SerializeField] private float talkLeanForward = 8f;
    [SerializeField] private float hunch = 0.12f;
    [SerializeField] private float breathing = 0.03f;
    [SerializeField] private float lookRange = 15f;
    [SerializeField] private float maxHeadYaw = 65f;
    [SerializeField] private Transform targetToWatch;

    private Quaternion workRestRot;
    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    private bool restCaptured;

    private float workWeight = 1f;
    private float talkWeight = 0f;
    private float pointWeight = 0f;

    private float headYaw;
    private float spineYawOffset;
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
    // METODI PUBBLICI DA CHIAMARE DALLO SCRIPT DEL DIALOGO DELL'INVENTORE
    // =========================================================================

    /// <summary>Chiama questo quando inizia il dialogo con il giocatore.</summary>
    public void StartTalking()
    {
        statoAttuale = StatoInventore.ParlandoConPlayer;
    }

    /// <summary>Chiama questo nella battuta in cui indica la cesta per i tronchi.</summary>
    public void PointAtBasket()
    {
        statoAttuale = StatoInventore.IndicandoCesta;
    }

    /// <summary>Chiama questo per smettere di indicare e tornare a parlare col giocatore.</summary>
    public void StopPointing()
    {
        statoAttuale = StatoInventore.ParlandoConPlayer;
    }

    /// <summary>Chiama questo quando il dialogo finisce e torna a segare il legno.</summary>
    public void ResumeWorking()
    {
        statoAttuale = StatoInventore.Lavorando;
    }

    // =========================================================================

    private void LateUpdate()
    {
        if (targetToWatch == null && Camera.main != null)
        {
            targetToWatch = Camera.main.transform;
        }

        float k = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

        // Calcola quanto è già allineato con la posizione di lavoro (0° = allineato)
        float angoloDalBanco = Quaternion.Angle(transform.rotation, workRestRot);
        bool prontoPerSegare = (statoAttuale == StatoInventore.Lavorando) && (angoloDalBanco < 12f);

        // Pesi fluidi dei 3 stati: inizia a segare solo quando ha quasi finito di girarsi
        workWeight = Mathf.Lerp(workWeight, prontoPerSegare ? 1f : 0f, k);
        talkWeight = Mathf.Lerp(talkWeight, (statoAttuale == StatoInventore.ParlandoConPlayer || (statoAttuale == StatoInventore.Lavorando && !prontoPerSegare)) ? 1f : 0f, k);
        pointWeight = Mathf.Lerp(pointWeight, statoAttuale == StatoInventore.IndicandoCesta ? 1f : 0f, k);

        // 1. Rotazione del corpo (Root) in base allo stato
        if (statoAttuale == StatoInventore.Lavorando)
        {
            // Torna fluido a guardare il banco/tronco su cui stava lavorando
            transform.rotation = Quaternion.Slerp(transform.rotation, workRestRot, k);
        }
        else if (statoAttuale == StatoInventore.ParlandoConPlayer && targetToWatch != null)
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
        else if (statoAttuale == StatoInventore.IndicandoCesta && basketTarget != null)
        {
            Vector3 toBasket = basketTarget.position - transform.position;
            toBasket.y = 0f;
            if (toBasket.sqrMagnitude > 0.001f)
            {
                Vector3 dir = invertForward ? -toBasket.normalized : toBasket.normalized;
                Quaternion desiredRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, bodyPointOffsetAngle + modelFacingOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, k);
            }
        }

        // 2. Calcolo angolo sguardo (Testa + Busto)
        float targetHeadYaw = 0f;
        float targetSpineYaw = 0f;

        Transform activeTarget = (statoAttuale == StatoInventore.IndicandoCesta && basketTarget != null)
            ? basketTarget
            : targetToWatch;

        if (activeTarget != null && statoAttuale != StatoInventore.Lavorando)
        {
            Vector3 toTarget = activeTarget.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist < (statoAttuale == StatoInventore.IndicandoCesta ? 300f : lookRange) && dist > 0.01f)
            {
                Vector3 local = transform.InverseTransformDirection(toTarget / dist);
                if (invertForward) local = -local;
                float signedAngle = Mathf.Atan2(local.x, Mathf.Max(0.01f, local.z)) * Mathf.Rad2Deg;

                if (statoAttuale == StatoInventore.IndicandoCesta)
                {
                    targetSpineYaw = Mathf.Clamp(signedAngle * 0.45f, -35f, 35f);
                    targetHeadYaw = Mathf.Clamp(signedAngle - targetSpineYaw, -maxHeadYaw, maxHeadYaw);
                }
                else
                {
                    targetHeadYaw = Mathf.Clamp(signedAngle, -maxHeadYaw, maxHeadYaw);
                    targetSpineYaw = targetHeadYaw * 0.2f;
                }
            }
        }

        headYaw = Mathf.Lerp(headYaw, targetHeadYaw, k);
        spineYawOffset = Mathf.Lerp(spineYawOffset, targetSpineYaw, k);

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
        workRestRot = transform.rotation; // Salva la direzione iniziale in cui segava il legno
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

        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.3f);
        float sawPhase = t * Mathf.PI * 2f * sawSpeed;
        float sawPush = Mathf.Sin(sawPhase); // Spinta avanti/indietro della sega

        // --- 1. BUSTO ---
        // Quando sega, il busto accompagna leggermente il movimento avanti/indietro
        float workLean = workLeanForward + (sawPush * 4.5f);
        float talkLean = talkLeanForward + (breath * 1.5f);
        float pointLean = talkLeanForward - 2f;

        float finalLean = (workLean * workWeight) + (talkLean * talkWeight) + (pointLean * pointWeight);
        float torsoY = (-hunch + breath * breathing - (workWeight * Mathf.Abs(sawPush) * 0.03f)) * invScale;

        spine.localPosition = spineRestPos + new Vector3(0f, torsoY, 0f);
        spine.localRotation = spineRestRot * Quaternion.Euler(
            finalLean,
            spineYawOffset + (workWeight * sawPush * 3f),
            Mathf.Sin(t * 0.8f) * 1.2f * (1f - workWeight)
        );

        // --- 2. TESTA ---
        // Quando lavora guarda in basso verso il legno, ma ogni tanto alza lo sguardo e si guarda intorno
        float lookAroundCycle = Mathf.PerlinNoise(t * 0.35f, 2.7f);
        float isGlancingUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.78f, lookAroundCycle));
        float workGlanceYaw = (Mathf.PerlinNoise(t * 0.55f, 9.1f) - 0.5f) * 2f * 45f * isGlancingUp;
        float workPitch = Mathf.Lerp(22f, -4f, isGlancingUp); // 22° in basso sul legno, -4° quando si guarda attorno

        // Quando parla annuisce leggermente con enfasi
        float talkNod = Mathf.Sin(t * 3.8f) * 3.5f * talkWeight;
        float talkTilt = Mathf.Sin(t * 1.4f) * 4.5f * talkWeight;

        float finalYaw = (workGlanceYaw * workWeight) + (headYaw * (talkWeight + pointWeight));
        float finalPitch = (workPitch * workWeight) + ((2f + talkNod) * talkWeight) + (-3f * pointWeight);
        float finalRoll = talkTilt;

        head.localPosition = headRestPos;
        head.localRotation = headRestRot * Quaternion.Euler(finalPitch, finalYaw, finalRoll);

        // --- 3. BRACCIA ---
        float handParentScale = handLeft.parent != null ? handLeft.parent.lossyScale.y : parentScale;
        float handInvScale = handParentScale > 0.0001f ? (1f / handParentScale) : 1f;

        // A) Posa Lavoro (Mano destra sega avanti/indietro, mano sinistra tiene fermo il tronco)
        Vector3 leftWorkPos = holdLogHandPos * handInvScale;
        Quaternion leftWorkRot = leftRestRot * Quaternion.Euler(-20f, 25f, -15f);

        Vector3 sawOffset = new Vector3(
            0f,
            Mathf.Cos(sawPhase) * sawVerticalArc,
            sawPush * sawStrokeLength
        ) * handInvScale;
        Vector3 rightWorkPos = (sawHandCenter * handInvScale) + sawOffset;
        Quaternion rightWorkRot = rightRestRot * Quaternion.Euler(sawGripEuler.x + (sawPush * 12f), sawGripEuler.y, sawGripEuler.z);

        // B) Posa Conversazione (Gesticola in modo vivace)
        float gPhase = t * Mathf.PI * 2f * gestureSpeed;
        Vector3 scaledTalkCenter = talkHandsCenter * handInvScale;
        float halfTalkGap = talkHandsGap * 0.5f * handInvScale;

        Vector3 leftTalkPos = scaledTalkCenter + new Vector3(
            -halfTalkGap - Mathf.Abs(Mathf.Sin(gPhase * 0.5f)) * 0.12f * handInvScale,
            (Mathf.Sin(gPhase) * gestureAmplitude + breath * breathing) * handInvScale,
            (Mathf.Cos(gPhase) * 0.08f) * handInvScale
        );
        Vector3 rightTalkPos = scaledTalkCenter + new Vector3(
            halfTalkGap + Mathf.Abs(Mathf.Cos(gPhase * 0.5f)) * 0.12f * handInvScale,
            (Mathf.Cos(gPhase * 0.9f) * gestureAmplitude + breath * breathing) * handInvScale,
            (Mathf.Sin(gPhase * 0.9f) * 0.08f) * handInvScale
        );

        Quaternion leftTalkRot = leftRestRot * Quaternion.Euler(-25f + Mathf.Sin(gPhase) * 15f, -15f, -22f);
        Quaternion rightTalkRot = rightRestRot * Quaternion.Euler(-25f + Mathf.Cos(gPhase * 0.9f) * 15f, 15f, 22f);

        // C) Posa Indica la Cesta
        Vector3 leftPointPos = scaledTalkCenter + new Vector3(-halfTalkGap, 0.05f * handInvScale, 0f);
        Quaternion leftPointRot = leftRestRot * Quaternion.Euler(-15f, -10f, -18f);

        Vector3 rightPointPos = pointingHandOffset * handInvScale;
        float aimYaw = headYaw + spineYawOffset * 0.5f;
        Quaternion rightPointRot = rightRestRot * Quaternion.Euler(pointingArmEuler.x, pointingArmEuler.y + aimYaw, pointingArmEuler.z);

        // Miscelazione finale tra i 3 stati
        handLeft.localPosition = (leftWorkPos * workWeight) + (leftTalkPos * talkWeight) + (leftPointPos * pointWeight);
        handRight.localPosition = (rightWorkPos * workWeight) + (rightTalkPos * talkWeight) + (rightPointPos * pointWeight);

        // Slerp progressivo per le rotazioni
        Quaternion blendedLeftRot = Quaternion.Slerp(leftWorkRot, leftTalkRot, talkWeight / Mathf.Max(0.001f, workWeight + talkWeight));
        blendedLeftRot = Quaternion.Slerp(blendedLeftRot, leftPointRot, pointWeight);

        Quaternion blendedRightRot = Quaternion.Slerp(rightWorkRot, rightTalkRot, talkWeight / Mathf.Max(0.001f, workWeight + talkWeight));
        blendedRightRot = Quaternion.Slerp(blendedRightRot, rightPointRot, pointWeight);

        handLeft.localRotation = blendedLeftRot;
        handRight.localRotation = blendedRightRot;
    }

    private void OnDisable()
    {
        RestoreRest();
    }
}