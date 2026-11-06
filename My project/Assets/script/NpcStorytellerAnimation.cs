using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class NpcStorytellerAnimation : MonoBehaviour
{
    // Mantiene i nomi originali per conservare i collegamenti.
    [Header("Articolazioni")]
    public Transform headBone;
    public Transform leftArmBone;
    public Transform rightArmBone;

    [Header("Dialogo e scheletro")]
    public NpcDialogue dialogue;
    public Transform bodyRoot;

    [Header("Respiro e tronco")]
    public float breathingSpeed = 2f;
    public float breathingAmount = 0.01f;
    public float bodySwaySpeed = 3f;
    public float bodySwayAngle = 1f;

    [Header("Gesticolazione")]
    public float gestureSpeed = 3.5f;
    public float gestureIntensity = 10f;
    public float lookDownAngle = 12f;
    public float lookSideAngle = 35f;
    public float smoothSpeed = 4f;

    public enum Mood { Idle, Listening, Thinking, Speaking }
    public Mood CurrentMood { get; private set; }

    private Quaternion headRest = Quaternion.identity;
    private Quaternion leftRest = Quaternion.identity;
    private Quaternion rightRest = Quaternion.identity;
    private Quaternion bodyRest = Quaternion.identity;
    private Vector3 bodyPosition;

    private bool initialized;
    private bool wasOpen;
    private bool wasRevealing;
    private string previousSpeech = "";
    private float speakingUntil;
    private bool handPositionsCaptured;
    private bool thinkingHandRaised;
    private Vector3 leftHandRestPosition;
    private Vector3 rightHandRestPosition;
    private float handMovementUnit;


    private void Start()
    {
        if (dialogue == null)
            dialogue = GetComponentInParent<NpcDialogue>();

        if (dialogue == null)
            dialogue = GetComponentInChildren<NpcDialogue>(true);

        Transform root = dialogue != null
            ? dialogue.transform : transform;

        if (headBone == null) headBone = FindBone(root, "Head");
        if (leftArmBone == null) leftArmBone = FindBone(root, "Arm_L");
        if (rightArmBone == null) rightArmBone = FindBone(root, "Arm_R");
        if (bodyRoot == null) bodyRoot = FindBone(root, "Main_Rig");

        // Anima lo scheletro, mantenendo fermo il trigger dell'NPC.
        if (bodyRoot == root) bodyRoot = null;

        if (headBone != null) headRest = headBone.localRotation;
        if (leftArmBone != null) leftRest = leftArmBone.localRotation;
        if (rightArmBone != null) rightRest = rightArmBone.localRotation;

        if (bodyRoot != null)
        {
            bodyRest = bodyRoot.localRotation;
            bodyPosition = bodyRoot.localPosition;
        }

        initialized = true;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        foreach (Transform bone in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == boneName) return bone;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!initialized) return;

        bool open = dialogue != null && dialogue.IsOpen;
        TMP_Text speech = dialogue == null ? null : dialogue.npcText;
        TMP_InputField input = dialogue == null
            ? null : dialogue.playerInputField;

        // Il dialogo disabilita questo campo mentre aspetta il server.
        bool thinking = open && input != null
            && input.gameObject.activeInHierarchy
            && !input.interactable;

        bool revealing = open && speech != null
            && speech.maxVisibleCharacters
               < speech.textInfo.characterCount;

        string text = speech == null ? "" : speech.text;
        float now = Time.unscaledTime;

        if (open && !thinking
            && (!wasOpen || text != previousSpeech)
            && !string.IsNullOrWhiteSpace(text))
        {
            int words = text.Split(
                new[] { ' ', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries).Length;

            speakingUntil = now + Mathf.Clamp(words / 3f, 2f, 8f);
        }

        // Dopo "Show all" termina il gesto con naturalezza.
        if (open && wasRevealing && !revealing)
            speakingUntil = Mathf.Max(speakingUntil, now + 1.4f);

        bool discussing = dialogue != null
            && dialogue.learningQuest != null
            && dialogue.learningQuest.IsOpen;

        if (!open)
            CurrentMood = discussing ? Mood.Listening : Mood.Idle;
        else if (thinking)
            CurrentMood = Mood.Thinking;
        else if (IsWriting())
            CurrentMood = Mood.Listening;
        else if (revealing || now < speakingUntil)
            CurrentMood = Mood.Speaking;
        else
            CurrentMood = Mood.Listening;

        wasOpen = open;
        wasRevealing = revealing;
        previousSpeech = text;

        Animate(now);
    }

    private bool IsWriting()
    {
        if (dialogue == null || dialogue.dialoguePanel == null
            || EventSystem.current == null)
            return false;

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;

        if (selected == null ||
            !selected.transform.IsChildOf(dialogue.dialoguePanel.transform))
            return false;

        TMP_InputField field = selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }

    private void Animate(float time)
    {
        Transform space = dialogue != null ? dialogue.transform : transform;

        if (!handPositionsCaptured)
        {
            if (leftArmBone != null)
                leftHandRestPosition = leftArmBone.localPosition;
            if (rightArmBone != null)
                rightHandRestPosition = rightArmBone.localPosition;

            float span = leftArmBone != null && rightArmBone != null
                ? Vector3.Distance(leftArmBone.position, rightArmBone.position) * 0.5f
                : 0f;
            float height = headBone != null
                ? Mathf.Abs(Vector3.Dot(headBone.position - space.position, space.up))
                : 0f;
            handMovementUnit = Mathf.Max(0.001f, span, height * 0.25f);
            handPositionsCaptured = true;
        }

        Vector3 head = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 right = Vector3.zero;
        float sway = 0.15f;
        float strength = Mathf.Clamp(gestureIntensity / 10f, 0.8f, 1.5f);

        if (CurrentMood != Mood.Thinking) thinkingHandRaised = false;

        if (CurrentMood == Mood.Speaking)
        {
            float a = Mathf.Sin(time * gestureSpeed);
            float b = Mathf.Sin(time * gestureSpeed * 0.85f + 1.6f);
            head = new Vector3(Mathf.Sin(time * 2f) * 3f,
                Mathf.Sin(time * 1.2f) * 5f, 0f);
            left = new Vector3(-30f + a * 25f * strength, 10f, 15f);
            right = new Vector3(-30f + b * 25f * strength, -10f, -15f);
            sway = 1.4f;
        }
        else if (CurrentMood == Mood.Thinking)
        {
            head = new Vector3(8f, -10f, -5f);
            left = new Vector3(-110f + (Mathf.Repeat(time, 3.6f) < 1.4f ? Mathf.Sin(time * 10f) * 18f : 0f), 15f, 20f);
            right = new Vector3(-55f, -15f, -15f);
        }
        else if (CurrentMood == Mood.Listening)
        {
            head = new Vector3(Mathf.Sin(time * 0.9f), 0f, 2f);
            left = new Vector3(-2f, 0f, 1f);
            right = new Vector3(-2f, 0f, -1f);
        }

        float blend = 1f - Mathf.Exp(
            -Mathf.Max(4f, smoothSpeed)
            * Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        if (bodyRoot != null)
        {
            float angle = Mathf.Clamp(bodySwayAngle, 0f, 2f) * sway;
            Rotate(bodyRoot, bodyRest * Quaternion.Euler(
                0f, Mathf.Sin(time * 1.5f) * angle,
                Mathf.Sin(time * 2f) * angle * 0.5f), blend);
            Vector3 position = bodyPosition + Vector3.up
                * Mathf.Sin(time * breathingSpeed)
                * Mathf.Clamp(breathingAmount, 0f, 0.015f);
            bodyRoot.localPosition =
                Vector3.Lerp(bodyRoot.localPosition, position, blend);
        }

        Rotate(headBone, headRest * Quaternion.Euler(head), blend);
        Rotate(leftArmBone, leftRest * Quaternion.Euler(left), blend);
        Rotate(rightArmBone, rightRest * Quaternion.Euler(right), blend);

        Vector3 targetLeft = leftHandRestPosition;
        Vector3 targetRight = rightHandRestPosition;

        if (headBone != null &&
            (CurrentMood == Mood.Speaking || CurrentMood == Mood.Thinking))
        {
            Vector3 up = space.up;
            Vector3 forward = space.forward;
            if (dialogue != null && dialogue.player != null)
            {
                Vector3 toward = Vector3.ProjectOnPlane(
                    dialogue.player.transform.position - headBone.position, up);
                if (toward.sqrMagnitude > 0.0001f) forward = toward.normalized;
            }
            Vector3 side = Vector3.Cross(up, forward).normalized;
            Vector3 anchor = headBone.position;
            float unit = handMovementUnit;
            Vector3 worldLeft;
            Vector3 worldRight;

            if (CurrentMood == Mood.Thinking)
            {
                // Prima alza il pugno lateralmente, poi lo porta sul capo.
                if (leftArmBone != null &&
                    Vector3.Dot(leftArmBone.position - anchor, up) > unit * 0.68f)
                    thinkingHandRaised = true;

                worldLeft = thinkingHandRaised
                    ? anchor - side * unit * 0.15f
                        + up * unit * 0.60f + forward * unit * 0.10f
                    : anchor - side * unit * 0.90f
                        + up * unit * 0.95f + forward * unit * 0.20f;

                worldRight = anchor + side * unit * 0.14f
                    + up * unit * 0.10f + forward * unit * 0.50f;
                worldRight += up * (Mathf.Sin(time * 1.2f) * unit * 0.01f);
                if (thinkingHandRaised)
                {
                    float scratch = Mathf.Repeat(time, 3.6f) < 1.4f
                        ? Mathf.Sin(time * 10f) : 0f;
                    worldLeft += (side * 0.10f + forward * 0.025f)
                        * unit * scratch;
                }
            }
            else
            {
                float a = Mathf.Sin(time * gestureSpeed);
                float b = Mathf.Sin(time * gestureSpeed * 0.85f + 1.6f);
                worldLeft = anchor - side * unit * 0.95f
                    + up * unit * (-0.25f + a * 0.38f * strength)
                    + forward * unit * 0.40f;
                worldRight = anchor + side * unit * 0.95f
                    + up * unit * (-0.25f + b * 0.38f * strength)
                    + forward * unit * 0.40f;
            }

            if (leftArmBone != null)
                targetLeft = leftArmBone.parent != null
                    ? leftArmBone.parent.InverseTransformPoint(worldLeft)
                    : worldLeft;
            if (rightArmBone != null)
                targetRight = rightArmBone.parent != null
                    ? rightArmBone.parent.InverseTransformPoint(worldRight)
                    : worldRight;
        }

        if (leftArmBone != null)
            leftArmBone.localPosition =
                Vector3.Lerp(leftArmBone.localPosition, targetLeft, blend);
        if (rightArmBone != null)
            rightArmBone.localPosition =
                Vector3.Lerp(rightArmBone.localPosition, targetRight, blend);
    }

    private static void Rotate(
        Transform bone, Quaternion target, float blend)
    {
        if (bone != null)
            bone.localRotation =
                Quaternion.Slerp(bone.localRotation, target, blend);
    }

    private void OnDisable()
    {
        if (!initialized) return;
        thinkingHandRaised = false;
        if (handPositionsCaptured)
        {
            if (leftArmBone != null)
                leftArmBone.localPosition = leftHandRestPosition;
            if (rightArmBone != null)
                rightArmBone.localPosition = rightHandRestPosition;
        }

        if (headBone != null) headBone.localRotation = headRest;
        if (leftArmBone != null) leftArmBone.localRotation = leftRest;
        if (rightArmBone != null) rightArmBone.localRotation = rightRest;

        if (bodyRoot != null)
        {
            bodyRoot.localRotation = bodyRest;
            bodyRoot.localPosition = bodyPosition;
        }
    }
}