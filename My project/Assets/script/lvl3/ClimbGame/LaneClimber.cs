using UnityEngine;

/// <summary>
/// The climber: climbs automatically, changes lane on input (IClimberInput, keyboard by default).
/// Handles hazard effects (hit, slip, gust). Stamina is in ClimberStamina, the animation in ClimberAnimator.
/// </summary>
public class LaneClimber : MonoBehaviour
{
    public ClimbWeather weather;

    [Header("Lanes")]
    public int laneCount = 4;
    public float laneWidth = 1.6f;
    [SerializeField] private float laneSwitchSpeed = 14f;

    [Header("Climbing")]
    [SerializeField] private float climbSpeed = 2.6f;

    [SerializeField] private ClimberStamina stamina = new ClimberStamina();

    [Header("Hazard effects")]
    [SerializeField] private float hitCost = 20f;
    [SerializeField] private float slipCost = 10f;
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

    public int Lane { get; private set; }
    public ClimberStamina Stamina => stamina;
    public float Height => transform.position.y;
    /// <summary>False during fades, the fall and after victory.</summary>
    public bool Active { get; set; }

    private IClimberInput input;
    private ClimberAnimator animator;
    private GameObject body;
    private float dropRemaining, stunTimer, invulnerableTimer;

    private void Awake()
    {
        // Another input component on the climber replaces the keyboard.
        input = GetComponent<IClimberInput>() ?? new KeyboardClimberInput();

        // The model has its arms spread: we fit it by height, it may overflow up to the lane width.
        body = ModelFactory.Create("Body", transform, model, PrimitiveType.Capsule, color,
                                   new Vector3(model != null ? laneWidth : size.x, size.y, size.x));
        body.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
        ModelFactory.SetMaterial(body, modelMaterial);

        animator = GetComponent<ClimberAnimator>();
        if (animator != null) animator.Bind(body.transform);
    }

    public float LaneX(int lane) => (lane - (laneCount - 1) / 2f) * laneWidth;

    /// <summary>Climber hitbox (a bit smaller than the visual, to be forgiving).</summary>
    public Bounds Bounds => new Bounds(transform.position, size * 0.75f);

    public void ResetAt(float height)
    {
        Lane = laneCount / 2;
        transform.position = new Vector3(LaneX(Lane), height, 0f);
        stamina.Reset();
        dropRemaining = stunTimer = invulnerableTimer = 0f;
        ModelFactory.SetVisible(body, true);
        if (animator != null) animator.ResetPose();
    }

    private void Update()
    {
        if (!Active) return;
        float dt = Time.deltaTime;

        int laneChange = input.ReadLaneChange();
        if (laneChange != 0) Push(laneChange);

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
        stamina.Drain(weather.Current.drainMultiplier, dt);

        // Blink while invincible.
        ModelFactory.SetVisible(body, invulnerableTimer <= 0f || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f);
    }

    // ---------- Effects ----------

    /// <summary>Changes lane (key or gust). Blocked at the edges.</summary>
    public void Push(int direction) => Lane = Mathf.Clamp(Lane + direction, 0, laneCount - 1);

    /// <summary>Rock or lightning: stamina loss, knockback and a short stun.</summary>
    public void Hit()
    {
        if (invulnerableTimer > 0f) return;
        stamina.Spend(hitCost);
        dropRemaining = hitDrop;
        stunTimer = stunTime;
        invulnerableTimer = invulnerableTime;
    }

    /// <summary>Wet patch: slide down.</summary>
    public void Slip()
    {
        if (invulnerableTimer > 0f) return;
        stamina.Spend(slipCost);
        dropRemaining = slipDrop;
        invulnerableTimer = invulnerableTime * 0.5f;
    }

    public void Refill(float amount) => stamina.Refill(amount);
}
