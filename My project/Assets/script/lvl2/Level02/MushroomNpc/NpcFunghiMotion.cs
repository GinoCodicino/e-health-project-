using UnityEngine;

/// <summary>
/// Uomo Fungo che parla stando FERMO: si muovono solo testa e mani (braccia).
/// Il corpo non cammina, non dondola e non ruota.
/// Mettilo su NPC_funghi (la radice, quella con Npc Dialogue).
/// </summary>
public class NpcFunghiMotion : MonoBehaviour
{
    [Header("Giocatore")]
    [Tooltip("Se vuoto cerca un oggetto con Tag 'Player', poi uno chiamato 'main_character'")]
    public Transform player;

    [Header("Quando parlare")]
    [Tooltip("Se vero parla quando il giocatore è entro 'Talk Distance'. " +
             "Se falso parla solo quando chiami StartTalking() (es. dal tuo NpcDialogue)")]
    public bool talkAutomaticallyWhenNear = true;
    [Tooltip("PER TEST: parla sempre, anche senza giocatore vicino")]
    public bool alwaysTalk = false;
    public float talkDistance = 3f;

    [Header("Mani / braccia")]
    public float armGestureAngle = 35f;
    public float armGestureSpeed = 5f;
    [Tooltip("Asse locale su cui ruotano le braccia. Se si muovono storte prova (0,0,1) o (0,1,0)")]
    public Vector3 armAxis = new Vector3(1, 0, 0);

    [Header("Testa")]
    public float headNodAngle = 8f;       // annuisce
    public float headTiltAngle = 5f;      // piega leggermente di lato
    [Tooltip("Asse locale per annuire. Se gira male prova (0,0,1)")]
    public Vector3 headAxis = new Vector3(1, 0, 0);
    [Tooltip("Gira solo la testa verso il giocatore mentre parla")]
    public bool headLooksAtPlayer = false;
    public float headLookMaxAngle = 40f;

    [Header("Ossa (trovate in automatico se vuote)")]
    public Transform armL, armR, head;

    public bool IsTalking { get; private set; }

    bool _forcedTalk;
    float _blend;
    Quaternion _armLRest, _armRRest, _headRest;

    void Awake()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("main_character");
            if (p != null) player = p.transform;
        }

        // Prende le ossa usate DAVVERO dalla mesh visibile (SkinnedMeshRenderer),
        // non quelle di un rig vuoto: NPC_funghi ha due Main_Rig annidati.
        armL = armL ? armL : FindSkinnedBone("Arm_L");
        armR = armR ? armR : FindSkinnedBone("Arm_R");
        head = head ? head : FindSkinnedBone("Head");

        Debug.Log($"[NpcFunghiMotion] Ossa trovate -> Arm_L: {Path(armL)} | Arm_R: {Path(armR)} | Head: {Path(head)} | Player: {(player ? player.name : "NESSUNO")}", this);

        if (armL) _armLRest = armL.localRotation;
        if (armR) _armRRest = armR.localRotation;
        if (head) _headRest = head.localRotation;

        // Un componente Animation legacy senza clip dà l'avviso "Default clip could not be found"
        var legacy = GetComponent<Animation>();
        if (legacy != null && legacy.clip == null) legacy.enabled = false;
    }

    void Update()
    {
        bool near = false;
        if (talkAutomaticallyWhenNear && player != null)
        {
            Vector3 d = player.position - transform.position; d.y = 0;
            near = d.magnitude <= talkDistance;
        }
        IsTalking = alwaysTalk || _forcedTalk || near;

        // Entrata/uscita morbida dall'animazione
        _blend = Mathf.MoveTowards(_blend, IsTalking ? 1f : 0f, Time.deltaTime * 3f);
    }

    // LateUpdate: così sovrascriviamo un eventuale Animator
    void LateUpdate()
    {
        float t = Time.time;

        // ---- Mani: gesticolano mentre parla ----
        Vector3 aAxis = armAxis.sqrMagnitude > 0 ? armAxis.normalized : Vector3.right;
        if (armL) armL.localRotation = _armLRest * Quaternion.AngleAxis(Gesture(t, 0f) * armGestureAngle * _blend, aAxis);
        if (armR) armR.localRotation = _armRRest * Quaternion.AngleAxis(Gesture(t, 1.7f) * armGestureAngle * _blend, aAxis);

        // ---- Testa: annuisce e si inclina ----
        if (head)
        {
            Vector3 hAxis = headAxis.sqrMagnitude > 0 ? headAxis.normalized : Vector3.right;
            float nod = Mathf.Sin(t * armGestureSpeed * 0.8f) * headNodAngle * _blend;
            float tilt = (Mathf.PerlinNoise(t * 0.7f, 5f) - 0.5f) * 2f * headTiltAngle * _blend;
            Quaternion q = _headRest * Quaternion.AngleAxis(nod, hAxis) * Quaternion.Euler(0, 0, tilt);

            if (headLooksAtPlayer && player != null && _blend > 0f)
            {
                // ruota solo la testa (asse verticale del mondo), limitata a headLookMaxAngle
                Vector3 to = player.position - head.position; to.y = 0;
                Vector3 fwd = transform.forward; fwd.y = 0;
                float yaw = Mathf.Clamp(Vector3.SignedAngle(fwd, to, Vector3.up), -headLookMaxAngle, headLookMaxAngle);
                head.localRotation = q;
                head.rotation = Quaternion.AngleAxis(yaw * _blend, Vector3.up) * head.rotation;
            }
            else head.localRotation = q;
        }
    }

    /// Movimento "da conversazione": onda + rumore, così non sembra un metronomo (0..1).
    float Gesture(float t, float seed)
    {
        float wave = Mathf.Sin(t * armGestureSpeed + seed * 3f) * 0.6f;
        float noise = (Mathf.PerlinNoise(t * 1.3f, seed) - 0.5f) * 1.6f;
        return Mathf.Clamp(wave + noise, -1f, 1f) * 0.5f + 0.5f;
    }

    Transform FindSkinnedBone(string name)
    {
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var b in smr.bones)
                if (b != null && b.name == name) return b;

        // Ripiego: l'osso con quel nome più in profondità nella gerarchia
        Transform best = null; int bestDepth = -1;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != name) continue;
            int d = 0; for (var p = t; p != null; p = p.parent) d++;
            if (d > bestDepth) { bestDepth = d; best = t; }
        }
        return best;
    }

    string Path(Transform t)
    {
        if (t == null) return "NON TROVATO";
        string s = t.name;
        for (var p = t.parent; p != null && p != transform; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    // ---- Da chiamare dal tuo script di dialogo (opzionale) ----
    public void StartTalking() => _forcedTalk = true;
    public void StopTalking() => _forcedTalk = false;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, talkDistance);
    }
}