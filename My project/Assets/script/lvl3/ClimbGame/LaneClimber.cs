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
    [Tooltip("Modèle 3D du grimpeur (le personnage principal). Vide = une capsule.")]
    public GameObject model;
    [Tooltip("Matériau imposé au modèle (ex. Texture_A, celui du personnage dans la montagne).")]
    public Material modelMaterial;
    [Tooltip("Rotation du modèle pour qu'il soit de dos, face à la paroi.")]
    [SerializeField] private float modelYaw = 0f;
    [SerializeField] private Color color = new Color(0.95f, 0.45f, 0.2f);
    [SerializeField] private Vector2 size = new Vector2(0.9f, 1.2f);

    [Header("Animation des mains")]
    [SerializeField] private string leftHandBone = "Arm_L";
    [SerializeField] private string rightHandBone = "Arm_R";
    [Tooltip("Hauteur dont une main monte pour attraper la paroi (m).")]
    [SerializeField] private float handReach = 0.45f;
    [Tooltip("Mouvements de mains par mètre grimpé.")]
    [SerializeField] private float handsPerMeter = 1.2f;
    [Tooltip("Inclinaison du corps pendant un changement de couloir (degrés).")]
    [SerializeField] private float leanAngle = 12f;

    public int Lane { get; private set; }
    public float Stamina { get; private set; }
    public float StaminaNormalized => Stamina / maxStamina;
    public float Height => transform.position.y;
    /// <summary>Faux pendant les fondus, la chute et après la victoire.</summary>
    public bool Active { get; set; }

    public event Action OnExhausted;

    private GameObject body;
    private Transform leftHand, rightHand;
    private Vector3 leftRest, rightRest;
    private float dropRemaining, stunTimer, invulnerableTimer;
    private float handPhase, lastY;

    private void Awake()
    {
        // Le modèle a les bras écartés : on le cale sur sa hauteur, il peut déborder jusqu'à la largeur du couloir.
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

    /// <summary>Hitbox du grimpeur (un peu plus petite que le visuel, pour être indulgent).</summary>
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
        ModelFactory.SetVisible(body, invulnerableTimer <= 0f || Mathf.Repeat(Time.time * 10f, 1f) > 0.4f);
    }

    private void LateUpdate()
    {
        Animate();
    }

    // ---------- Animation ----------

    // Les mains suivent la montée réelle : elles s'arrêtent quand il est étourdi
    // et repartent en arrière quand il glisse. Le corps penche du côté où il change de couloir.
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

    // On déplace la main en coordonnées monde (vers le haut), quel que soit l'axe de l'os.
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
