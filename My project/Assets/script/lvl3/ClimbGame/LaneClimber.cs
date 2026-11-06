using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The climber: climbs automatically, changes lane with ←/→ (or A/D).
/// Handles stamina and hazard effects (hit, slip, gust).
/// </summary>
public class LaneClimber : MonoBehaviour
{
    public ClimbWeather weather;

    [Header("Lanes")]
    public int laneCount = 4;
    public float laneWidth = 1.6f;
    [SerializeField] private float laneSwitchSpeed = 14f;

    [Header("Climbing")]
    [SerializeField] private float climbSpeed = 2.2f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainPerSecond = 2.5f;
    [SerializeField] private float hitCost = 20f;
    [SerializeField] private float slipCost = 10f;

    [Header("Hazard effects")]
    [Tooltip("Knockback after a rock or lightning hit (m).")]
    [SerializeField] private float hitDrop = 1.5f;
    [Tooltip("Slide on a wet patch (m).")]
    [SerializeField] private float slipDrop = 2.5f;
    [SerializeField] private float dropSpeed = 8f;
    [SerializeField] private float stunTime = 0.6f;
    [Tooltip("Invincibility time after a hit, to avoid chained damage.")]
    [SerializeField] private float invulnerableTime = 1.2f;

    [Header("Visual")]
    [Tooltip("3D model of the climber (the main character). Empty = a capsule.")]
    public GameObject model;
    [Tooltip("Material forced on the model (e.g. Texture_A, the one used by the character on the mountain).")]
    public Material modelMaterial;
    [Tooltip("Model rotation so it is seen from behind, facing the wall.")]
    [SerializeField] private float modelYaw = 0f;
    [SerializeField] private Color color = new Color(0.95f, 0.45f, 0.2f);
    [SerializeField] private Vector2 size = new Vector2(0.9f, 1.2f);

    [Header("Hand animation")]
    [SerializeField] private string leftHandBone = "Arm_L";
    [SerializeField] private string rightHandBone = "Arm_R";
    [Tooltip("How high a hand reaches up to grab the wall (m).")]
    [SerializeField] private float handReach = 0.45f;
    [Tooltip("Hand movements per meter climbed.")]
    [SerializeField] private float handsPerMeter = 1.2f;
    [Tooltip("Body tilt while changing lanes (degrees).")]
    [SerializeField] private float leanAngle = 12f;

    public int Lane { get; private set; }
    public float Stamina { get; private set; }
    public float StaminaNormalized => Stamina / maxStamina;
    public float Height => transform.position.y;
    /// <summary>False during fades, the fall and after victory.</summary>
    public bool Active { get; set; }

    public event Action OnExhausted;

    private GameObject body;
    private Transform leftHand, rightHand;
    private Vector3 leftRest, rightRest;
    private float dropRemaining, stunTimer, invulnerableTimer;
    private float handPhase, lastY;

    private void Awake()
    {
        // The model has its arms spread: we fit it by height, it may overflow up to the lane width.
        body = ModelFactory.Create("Body", transform, model, PrimitiveType.Capsule, color,
                                   new Vector3(model != null ? laneWidth : size.x, size.y, size.x));
        body.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
        ModelFactory.SetMaterial(body, modelMaterial);

        leftHand = FindBone(body.transform, leftHandBone);
        rightHand = FindBone(body.transform, rightHandBone);
        if (leftHand != null) leftRest = leftHand.localPosition;
        if (rightHand != null) rightRest = rightHand.localPosition;
    }

    public float LaneX(int lane) => (lane - (laneCount - 1) / 2f) * laneWidth;

    /// <summary>Climber hitbox (a bit smaller than the visual, to be forgiving).</summary>
    public Bounds Bounds => new Bounds(transform.position, size * 0.75f);

    public void ResetAt(float height)
    {
        Lane = laneCount / 2;
        transform.position = new Vector3(LaneX(Lane), height, 0f);
        Stamina = maxStamina;
        dropRemaining = stunTimer = invulnerableTimer = 0f;
        handPhase = 0f;
        lastY = height;
        ModelFactory.SetVisible(body, true);
    }

    private void Update()
    {
        if (!Active) return;
        float dt = Time.deltaTime;

        ReadInput();

        // Slide toward the target lane.
        Vector3 p = transform.position;
        p.x = Mathf.MoveTowards(p.x, LaneX(Lane), laneSwitchSpeed * dt);

        // Knockback (hit, slip) > stunned > normal climb.
        if (dropRemaining > 0f)
        {
            float d = Mathf.Min(dropRemaining, dropSpeed * dt);
            p.y -= d;
            dropRemaining -= d;
        }
        else if (stunTimer > 0f)
            stunTimer -= dt;
        else
            p.y += climbSpeed * weather.Current.climbSpeedMultiplier * dt;
        transform.position = p;

        invulnerableTimer -= dt;
        Spend(drainPerSecond * weather.Current.drainMultiplier * dt);

        // Blink while invincible.
        ModelFactory.SetVisible(body, invulnerableTimer <= 0f || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f);
    }

    private void LateUpdate()
    {
        Animate();
    }

    // ---------- Animation ----------

    // The hands follow the actual climb: they stop when he is stunned
    // and move backward when he slips. The body leans toward the lane he is moving to.
    private void Animate()
    {
        float climbed = transform.position.y - lastY;
        lastY = transform.position.y;
        handPhase += climbed * handsPerMeter * Mathf.PI;

        float s = Mathf.Sin(handPhase);
        PlaceHand(leftHand, leftRest, handReach * (0.5f + 0.5f * s));
        PlaceHand(rightHand, rightRest, handReach * (0.5f - 0.5f * s));

        float towardLane = LaneX(Lane) - transform.position.x;
        float lean = Mathf.Clamp(towardLane / laneWidth, -1f, 1f) * leanAngle;
        float sway = Mathf.Sin(handPhase) * 3f;
        body.transform.localRotation = Quaternion.Euler(0f, modelYaw, -lean + sway);
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

    private void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Push(-1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Push(1);
    }

    // ---------- Effects ----------

    /// <summary>Changes lane (key or gust). Blocked at the edges.</summary>
    public void Push(int direction) => Lane = Mathf.Clamp(Lane + direction, 0, laneCount - 1);

    /// <summary>Rock or lightning: stamina loss, knockback and a short stun.</summary>
    public void Hit()
    {
        if (invulnerableTimer > 0f) return;
        Spend(hitCost);
        dropRemaining = hitDrop;
        stunTimer = stunTime;
        invulnerableTimer = invulnerableTime;
    }

    /// <summary>Wet patch: slide down.</summary>
    public void Slip()
    {
        if (invulnerableTimer > 0f) return;
        Spend(slipCost);
        dropRemaining = slipDrop;
        invulnerableTimer = invulnerableTime * 0.5f;
    }

    public void Refill(float amount) => Stamina = Mathf.Min(maxStamina, Stamina + amount);

    private void Spend(float amount)
    {
        if (Stamina <= 0f) return;
        Stamina = Mathf.Max(0f, Stamina - amount);
        if (Stamina <= 0f) OnExhausted?.Invoke();
    }
}
