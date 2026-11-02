using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Le grimpeur : monte tout seul, change de couloir avec ←/→ (ou A/D).
/// Gère l'endurance et les effets des dangers (choc, glissade, rafale).
/// </summary>
public class LaneClimber : MonoBehaviour
{
    public ClimbWeather weather;

    [Header("Couloirs")]
    public int laneCount = 4;
    public float laneWidth = 1.6f;
    [SerializeField] private float laneSwitchSpeed = 14f;

    [Header("Montée")]
    [SerializeField] private float climbSpeed = 2.2f;

    [Header("Endurance")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainPerSecond = 2.5f;
    [SerializeField] private float hitCost = 20f;
    [SerializeField] private float slipCost = 10f;

    [Header("Effets des dangers")]
    [Tooltip("Recul après une pierre ou un éclair (m).")]
    [SerializeField] private float hitDrop = 1.5f;
    [Tooltip("Glissade sur une plaque mouillée (m).")]
    [SerializeField] private float slipDrop = 2.5f;
    [SerializeField] private float dropSpeed = 8f;
    [SerializeField] private float stunTime = 0.6f;
    [Tooltip("Temps d'invincibilité après un choc, pour ne pas enchaîner les dégâts.")]
    [SerializeField] private float invulnerableTime = 1.2f;

    [Header("Visuel")]
    public Sprite sprite;
    [SerializeField] private Color color = new Color(0.95f, 0.45f, 0.2f);
    [SerializeField] private Vector2 size = new Vector2(0.9f, 1.2f);

    public int Lane { get; private set; }
    public float Stamina { get; private set; }
    public float StaminaNormalized => Stamina / maxStamina;
    public float Height => transform.position.y;
    /// <summary>Faux pendant les fondus, la chute et après la victoire.</summary>
    public bool Active { get; set; }

    public event Action OnExhausted;

    private SpriteRenderer body;
    private float dropRemaining, stunTimer, invulnerableTimer;

    private void Awake()
    {
        body = SpriteFactory.Create("Body", transform, sprite, SpriteFactory.Square, color, size, 5);
    }

    public float LaneX(int lane) => (lane - (laneCount - 1) / 2f) * laneWidth;

    /// <summary>Hitbox du grimpeur (un peu plus petite que le visuel, pour être indulgent).</summary>
    public Bounds Bounds => new Bounds(transform.position, size * 0.75f);

    public void ResetAt(float height)
    {
        Lane = laneCount / 2;
        transform.position = new Vector3(LaneX(Lane), height, 0f);
        Stamina = maxStamina;
        dropRemaining = stunTimer = invulnerableTimer = 0f;
        body.color = sprite != null ? Color.white : color;
    }

    private void Update()
    {
        if (!Active) return;
        float dt = Time.deltaTime;

        ReadInput();

        // Glisse vers son couloir.
        Vector3 p = transform.position;
        p.x = Mathf.MoveTowards(p.x, LaneX(Lane), laneSwitchSpeed * dt);

        // Recul (choc, glissade) > étourdi > montée normale.
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

        // Clignote quand il est invincible.
        body.enabled = invulnerableTimer <= 0f || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f;
    }

    private void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Push(-1);
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Push(1);
    }

    // ---------- Effets ----------

    /// <summary>Change de couloir (touche ou rafale). Bloqué aux bords.</summary>
    public void Push(int direction) => Lane = Mathf.Clamp(Lane + direction, 0, laneCount - 1);

    /// <summary>Pierre ou éclair : perte d'endurance, recul et court étourdissement.</summary>
    public void Hit()
    {
        if (invulnerableTimer > 0f) return;
        Spend(hitCost);
        dropRemaining = hitDrop;
        stunTimer = stunTime;
        invulnerableTimer = invulnerableTime;
    }

    /// <summary>Plaque mouillée : on glisse vers le bas.</summary>
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
