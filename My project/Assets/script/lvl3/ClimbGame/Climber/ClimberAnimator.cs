using UnityEngine;

/// <summary>
/// Procedural climbing animation: the hands follow the actual climb (they stop when he is stunned
/// and move backward when he slips). The body leans toward the lane he is moving to.
/// </summary>
[RequireComponent(typeof(LaneClimber))]
public class ClimberAnimator : MonoBehaviour
{
    [SerializeField] private string leftHandBone = "Arm_L";
    [SerializeField] private string rightHandBone = "Arm_R";
    [Tooltip("How high a hand reaches up to grab the wall (m).")]
    [SerializeField] private float handReach = 0.45f;
    [Tooltip("Hand movements per meter climbed.")]
    [SerializeField] private float handsPerMeter = 1.2f;
    [Tooltip("Body tilt while changing lanes (degrees).")]
    [SerializeField] private float leanAngle = 12f;

    private LaneClimber climber;
    private Transform body, leftHand, rightHand;
    private Quaternion bodyRest;
    private Vector3 leftRest, rightRest;
    private float handPhase, lastY;

    /// <summary>Called by LaneClimber once the body model is created.</summary>
    public void Bind(Transform bodyRoot)
    {
        climber = GetComponent<LaneClimber>();
        body = bodyRoot;
        bodyRest = body.localRotation;
        leftHand = FindBone(body, leftHandBone);
        rightHand = FindBone(body, rightHandBone);
        if (leftHand != null) leftRest = leftHand.localPosition;
        if (rightHand != null) rightRest = rightHand.localPosition;
    }

    /// <summary>After a teleport (start, checkpoint): no hand movement for the jump.</summary>
    public void ResetPose()
    {
        handPhase = 0f;
        lastY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (body == null) return;

        float climbed = transform.position.y - lastY;
        lastY = transform.position.y;
        handPhase += climbed * handsPerMeter * Mathf.PI;

        float s = Mathf.Sin(handPhase);
        PlaceHand(leftHand, leftRest, handReach * (0.5f + 0.5f * s));
        PlaceHand(rightHand, rightRest, handReach * (0.5f - 0.5f * s));

        float towardLane = climber.LaneX(climber.Lane) - transform.position.x;
        float lean = Mathf.Clamp(towardLane / climber.laneWidth, -1f, 1f) * leanAngle;
        float sway = s * 3f;
        body.localRotation = bodyRest * Quaternion.Euler(0f, 0f, -lean + sway);
    }

    // Move the hand in world space (upward), whatever the bone's axis.
    private static void PlaceHand(Transform hand, Vector3 rest, float up)
    {
        if (hand == null) return;
        hand.localPosition = rest;
        hand.position += Vector3.up * up;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name == boneName) return t;
        return null;
    }
}
