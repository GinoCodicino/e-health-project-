using UnityEngine;

// The original movement script runs first; this only adapts its thinking pose to the hat.
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class HerbalistAppearance : MonoBehaviour
{
    [Header("Posa con il cappello")]
    public bool adaptThinkingPose = true;
    public Transform headTarget;
    public Transform faceTarget;
    public Transform liftTarget;
    public Transform leftHand;
    public Transform rightHand;
    public NpcStorytellerAnimation animationDriver;
    [HideInInspector] public Vector3 leftFistOffset;
    [HideInInspector] public Vector3 rightFistOffset;
    [HideInInspector] public float headSize;

    // Saved per NPC so the editor's Restore command can put its original appearance back.
    [HideInInspector] public SkinnedMeshRenderer originalRenderer;
    [HideInInspector] public Mesh originalMesh;
    [HideInInspector] public Material[] originalMaterials;
    [HideInInspector] public GameObject[] createdRoots;

    private bool poseActive;
    private bool initialized;
    private float began;
    private Vector3 startLeft, startRight;
    private Vector3 restLeft, restRight;

    private void Start()
    {
        if (animationDriver == null)
            animationDriver = GetComponentInChildren<NpcStorytellerAnimation>();
        if (leftHand != null) restLeft = leftHand.localPosition;
        if (rightHand != null) restRight = rightHand.localPosition;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!adaptThinkingPose || animationDriver == null || !animationDriver.isActiveAndEnabled
            || animationDriver.CurrentMood != NpcStorytellerAnimation.Mood.Thinking
            || headTarget == null || faceTarget == null || liftTarget == null
            || leftHand == null || rightHand == null)
        {
            poseActive = false;
            return;
        }
        float now = Time.unscaledTime;
        if (!poseActive)
        {
            poseActive = true;
            began = now;
            startLeft = leftHand.TransformPoint(leftFistOffset);
            startRight = rightHand.TransformPoint(rightFistOffset);
        }
        float elapsed = now - began;
        Vector3 leftCenter;
        if (elapsed < 0.55f)
            leftCenter = Vector3.Lerp(startLeft, liftTarget.position, Mathf.SmoothStep(0f, 1f, elapsed / 0.55f));
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 0.55f) / 0.45f));
            leftCenter = Vector3.Lerp(liftTarget.position, headTarget.position, t);
            float scratch = Mathf.Repeat(Mathf.Max(0f, elapsed - 1f), 3.6f) < 1.4f
                ? Mathf.Sin((elapsed - 1f) * 10f) : 0f;
            leftCenter += headTarget.right * (scratch * headSize * 0.085f * t);
        }
        Vector3 rightCenter = Vector3.Lerp(startRight, faceTarget.position,
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.7f)));
        leftHand.position = leftCenter - leftHand.TransformVector(leftFistOffset);
        rightHand.position = rightCenter - rightHand.TransformVector(rightFistOffset);
    }

    private void OnDisable()
    {
        if (initialized && poseActive)
        {
            if (leftHand != null) leftHand.localPosition = restLeft;
            if (rightHand != null) rightHand.localPosition = restRight;
        }
        poseActive = false;
    }
}
