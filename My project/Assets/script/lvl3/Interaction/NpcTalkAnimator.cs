using UnityEngine;

/// <summary>
/// Procedural talking animation for the cubic NPCs (bones Spine00, Head, Arm_L, Arm_R).
/// While the NPC speaks: hand gestures on a rhythm, head nods, sometimes a shrug.
/// While someone else speaks: listening pose with small nods.
/// Works alone or on top of an idle animator (e.g. EthanBrooksAnimator): it blends in during
/// the dialogue and gives the bones back afterwards.
///
/// With an Npc on the same object it starts and stops by itself. Other NPC systems can call
/// StartTalking / StartListening / StopTalking.
/// </summary>
[DefaultExecutionOrder(100)] // après les animations d'attente, pour pouvoir les recouvrir
public class NpcTalkAnimator : MonoBehaviour
{
    public enum TalkState { Idle, Talking, Listening }
    public enum ListeningPose { HandsDown, HandsTogether, HandsOnHips }

    [Header("Bones (found by name if empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("Personality")]
    [Tooltip("Size of the hand gestures (0 = barely moves, 1 = normal, 1.5 = theatrical).")]
    [Range(0f, 2f)] [SerializeField] private float gestureSize = 1f;
    [Tooltip("Gestures per second while talking.")]
    [Range(0.5f, 3f)] [SerializeField] private float gestureRate = 1.3f;
    [Tooltip("Chance that a gesture is a shrug (\"who knows?\", \"it has always changed\").")]
    [Range(0f, 1f)] [SerializeField] private float shrugChance = 0.2f;
    [Tooltip("Chance that a gesture is a pointing hand.")]
    [Range(0f, 1f)] [SerializeField] private float pointChance = 0.2f;
    [Tooltip("How much the head nods on each gesture (degrees).")]
    [SerializeField] private float nodAmount = 4f;
    [Tooltip("Chest pushed forward while talking (degrees, negative = leaning back, sure of himself).")]
    [SerializeField] private float talkLean = -3f;
    [SerializeField] private ListeningPose listeningPose = ListeningPose.HandsTogether;

    [Header("Blending")]
    [Tooltip("Seconds to blend in and out of the dialogue animation.")]
    [SerializeField] private float blendTime = 0.3f;
    [Tooltip("Seconds to go from one gesture to the next.")]
    [SerializeField] private float gestureTransition = 0.28f;

    // Positions de référence des mains, dans l'espace du torse (personnages cubiques du pack).
    private static readonly Vector3 HandsNeutral = new Vector3(1.1f, 1.3f, 1.4f);
    private static readonly Vector3 HandsOpen = new Vector3(1.8f, 1.6f, 1.6f);
    private static readonly Vector3 HandsShrug = new Vector3(2.0f, 1.8f, 1.2f);
    private static readonly Vector3 HandsPoint = new Vector3(0.6f, 1.7f, 2.6f);
    private static readonly Vector3 HandsDownPose = new Vector3(2.4f, 0.9f, 0.2f);
    private static readonly Vector3 HandsTogetherPose = new Vector3(0.55f, 1.3f, 1.4f);
    private static readonly Vector3 HandsHipsPose = new Vector3(2.0f, 0.9f, -0.5f);

    private TalkState state = TalkState.Idle;
    private float weight;
    private float stateTime;
    private bool restCaptured;
    private int seed;

    private Vector3 spineRestPos, headRestPos, leftRestPos, rightRestPos;
    private Quaternion spineRestRot, headRestRot, leftRestRot, rightRestRot;
    // Pose écrite à l'image précédente : si personne d'autre n'a bougé les os depuis, on repart de la pose de repos.
    private Vector3 lastSpinePos, lastHeadPos, lastLeftPos, lastRightPos;
    private Quaternion lastSpineRot, lastHeadRot, lastLeftRot, lastRightRot;
    private bool wroteLastFrame;

    private Npc npc;
    private bool inOwnDialogue;

    public TalkState State => state;

    private void Awake()
    {
        FindBones();
        CaptureRest();
        seed = Random.Range(0, 10000);
        npc = GetComponent<Npc>();
    }

    private void OnEnable()
    {
        if (npc != null) { npc.DialogueStarted += OnDialogueStarted; npc.DialogueEnded += OnDialogueEnded; }
        DialogueRunner.LineShown += OnLineShown;
        DialogueRunner.Closed += OnDialogueEnded;
    }

    private void OnDisable()
    {
        if (npc != null) { npc.DialogueStarted -= OnDialogueStarted; npc.DialogueEnded -= OnDialogueEnded; }
        DialogueRunner.LineShown -= OnLineShown;
        DialogueRunner.Closed -= OnDialogueEnded;
        if (wroteLastFrame) RestoreRest();
        weight = 0f; state = TalkState.Idle; inOwnDialogue = false; wroteLastFrame = false;
    }

    private void OnDialogueStarted() { inOwnDialogue = true; StartListening(); }
    private void OnDialogueEnded() { if (!inOwnDialogue) return; inOwnDialogue = false; StopTalking(); }
    private void OnLineShown(DialogueLine line, bool npcSpeaking)
    {
        if (!inOwnDialogue) return;
        if (npcSpeaking) StartTalking(); else StartListening();
    }

    /// <summary>The NPC speaks: gestures and nods.</summary>
    public void StartTalking() => SetState(TalkState.Talking);
    /// <summary>Someone else speaks: listening pose.</summary>
    public void StartListening() => SetState(TalkState.Listening);
    /// <summary>Back to normal (blends out, the idle animation takes over again).</summary>
    public void StopTalking() => SetState(TalkState.Idle);

    private void SetState(TalkState s)
    {
        if (s == state) return;
        state = s;
        stateTime = 0f;
        seed++; // nouvelle suite de gestes à chaque réplique
    }

    private void LateUpdate()
    {
        if (!restCaptured) return;
        stateTime += Time.deltaTime;
        float target = state == TalkState.Idle ? 0f : 1f;
        weight = Mathf.MoveTowards(weight, target, Time.deltaTime / Mathf.Max(0.01f, blendTime));
        if (weight <= 0f && state == TalkState.Idle)
        {
            // Rend la main : si personne d'autre n'anime les os, on les remet au repos.
            if (wroteLastFrame && !OtherAnimatorMovedBones()) RestoreRest();
            wroteLastFrame = false;
            return;
        }

        if (wroteLastFrame && !OtherAnimatorMovedBones()) RestoreRest();
        Apply(state == TalkState.Idle ? lastActiveState : state, stateTime, Mathf.SmoothStep(0f, 1f, weight));
        if (state != TalkState.Idle) lastActiveState = state;
        RememberWrittenPose();
    }

    private TalkState lastActiveState = TalkState.Listening;

    /// <summary>Poses the bones for a state at time t, blended over the current pose by w (used for previews too).</summary>
    public void Apply(TalkState s, float t, float w)
    {
        if (!restCaptured || w <= 0f) return;
        Vector3 spineP, headEuler, lP, rP; float spinePitch; Vector3 lRot, rRot;
        if (s == TalkState.Talking) TalkPose(t, out spineP, out spinePitch, out headEuler, out lP, out rP, out lRot, out rRot);
        else ListenPose(t, out spineP, out spinePitch, out headEuler, out lP, out rP, out lRot, out rRot);

        spine.localPosition = Vector3.Lerp(spine.localPosition, spineRestPos + spineP, w);
        spine.localRotation = Quaternion.Slerp(spine.localRotation, spineRestRot * Quaternion.Euler(spinePitch, 0f, 0f), w);
        head.localRotation = Quaternion.Slerp(head.localRotation, headRestRot * Quaternion.Euler(headEuler), w);
        handLeft.localPosition = Vector3.Lerp(handLeft.localPosition, lP, w);
        handRight.localPosition = Vector3.Lerp(handRight.localPosition, rP, w);
        handLeft.localRotation = Quaternion.Slerp(handLeft.localRotation, leftRestRot * Quaternion.Euler(lRot), w);
        handRight.localRotation = Quaternion.Slerp(handRight.localRotation, rightRestRot * Quaternion.Euler(rRot), w);
    }

    // --- Parler : un geste par "temps", choisi au hasard (reproductible) ---------------------------
    private void TalkPose(float t, out Vector3 spineP, out float spinePitch, out Vector3 headEuler, out Vector3 lP, out Vector3 rP, out Vector3 lRot, out Vector3 rRot)
    {
        float beatLen = 1f / gestureRate;
        int beat = Mathf.FloorToInt(t / beatLen);
        float inBeat = t - beat * beatLen;
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inBeat / gestureTransition));

        Gesture(beat - 1, out var lA, out var rA, out var shrugA, out var pitchA);
        Gesture(beat, out var lB, out var rB, out var shrugB, out var pitchB);
        Vector3 l = Vector3.Lerp(lA, lB, blend), r = Vector3.Lerp(rA, rB, blend);
        float shrug = Mathf.Lerp(shrugA, shrugB, blend);

        // Petit mouvement continu pour ne jamais être figé.
        float n1 = Mathf.PerlinNoise(t * 1.7f, seed * 0.13f) - 0.5f, n2 = Mathf.PerlinNoise(seed * 0.29f, t * 1.5f) - 0.5f;
        l += new Vector3(n1 * 0.3f, n2 * 0.3f, n2 * 0.2f) * gestureSize;
        r += new Vector3(n2 * 0.3f, n1 * 0.3f, n1 * 0.2f) * gestureSize;
        lP = new Vector3(-l.x, l.y, l.z);
        rP = r;

        // Hochement de tête en début de geste, inclinaison quand il hausse les épaules.
        float nod = Mathf.Sin(Mathf.Clamp01(inBeat / (beatLen * 0.6f)) * Mathf.PI) * nodAmount;
        float yaw = (Mathf.PerlinNoise(t * 0.5f, seed) - 0.5f) * 16f;
        headEuler = new Vector3(nod - shrug * 2f + Mathf.Lerp(pitchA, pitchB, blend), yaw, shrug * 9f);
        spineP = new Vector3(0f, shrug * 0.12f + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.02f, 0f);
        spinePitch = talkLean + nod * 0.3f;
        lRot = new Vector3(0f, 0f, -shrug * 25f);
        rRot = new Vector3(0f, 0f, shrug * 25f);
    }

    private void Gesture(int beat, out Vector3 left, out Vector3 right, out float shrug, out float headPitch)
    {
        float h = Hash(beat);
        float size = Mathf.Clamp(gestureSize, 0f, 1.5f);
        shrug = 0f; headPitch = 0f;
        if (h < shrugChance)
        {
            left = right = Vector3.LerpUnclamped(HandsNeutral, HandsShrug, size);
            shrug = 1f; headPitch = -1f;
        }
        else if (h < shrugChance + pointChance)
        {
            // Une main désigne le joueur, l'autre reste près du corps.
            bool rightHand = Hash(beat + 777) > 0.5f;
            Vector3 point = Vector3.LerpUnclamped(HandsNeutral, HandsPoint, size);
            left = rightHand ? HandsNeutral : point;
            right = rightHand ? point : HandsNeutral;
            headPitch = 2f;
        }
        else
        {
            // Mains ouvertes, chacune un peu différente, plus ou moins écartées.
            float open = Mathf.Lerp(0.15f, 1f, Hash(beat + 333)) * size;
            left = Vector3.LerpUnclamped(HandsNeutral, HandsOpen, open * Mathf.Lerp(0.6f, 1.2f, Hash(beat + 11)));
            right = Vector3.LerpUnclamped(HandsNeutral, HandsOpen, open * Mathf.Lerp(0.6f, 1.2f, Hash(beat + 23)));
        }
    }

    // --- Écouter --------------------------------------------------------------------------------
    private void ListenPose(float t, out Vector3 spineP, out float spinePitch, out Vector3 headEuler, out Vector3 lP, out Vector3 rP, out Vector3 lRot, out Vector3 rRot)
    {
        Vector3 hands = listeningPose == ListeningPose.HandsTogether ? HandsTogetherPose
                      : listeningPose == ListeningPose.HandsOnHips ? HandsHipsPose : HandsDownPose;
        float breath = Mathf.Sin(t * Mathf.PI * 2f * 0.25f);
        lP = new Vector3(-hands.x, hands.y + breath * 0.04f, hands.z);
        rP = new Vector3(hands.x, hands.y + breath * 0.04f, hands.z);
        // Petit hochement de tête toutes les 2,5 s environ.
        float c = Mathf.Repeat(t + seed * 0.37f, 2.6f);
        float nod = c < 0.5f ? Mathf.Sin(c / 0.5f * Mathf.PI) * 5f : 0f;
        headEuler = new Vector3(nod, (Mathf.PerlinNoise(t * 0.3f, seed) - 0.5f) * 10f, 4f);
        spineP = new Vector3(0f, breath * 0.03f, 0f);
        spinePitch = talkLean * 0.5f;
        lRot = rRot = Vector3.zero;
    }

    private float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 0x9E3779B1u ^ ((uint)seed + 0x6A09E667u) * 0x85EBCA6Bu;
            x ^= x >> 16; x *= 0x7FEB352Du;
            x ^= x >> 15; x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // --- Os et pose de repos --------------------------------------------------------------------
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
        if (spine == null || head == null || handLeft == null || handRight == null)
        {
            Debug.LogWarning($"{name}: NpcTalkAnimator needs the bones Spine00, Head, Arm_L and Arm_R.", this);
            return;
        }
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

    private bool OtherAnimatorMovedBones()
    {
        return spine.localPosition != lastSpinePos || spine.localRotation != lastSpineRot
            || head.localPosition != lastHeadPos || head.localRotation != lastHeadRot
            || handLeft.localPosition != lastLeftPos || handLeft.localRotation != lastLeftRot
            || handRight.localPosition != lastRightPos || handRight.localRotation != lastRightRot;
    }

    private void RememberWrittenPose()
    {
        lastSpinePos = spine.localPosition; lastSpineRot = spine.localRotation;
        lastHeadPos = head.localPosition; lastHeadRot = head.localRotation;
        lastLeftPos = handLeft.localPosition; lastLeftRot = handLeft.localRotation;
        lastRightPos = handRight.localPosition; lastRightRot = handRight.localRotation;
        wroteLastFrame = true;
    }
}
