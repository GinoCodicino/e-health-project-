using UnityEngine;

/// <summary>
/// Il fungaiolo resta FERMO al suo posto.
/// Quando il giocatore entra nel raggio:
///  - si gira verso di lui (solo rotazione, non si sposta)
///  - fa i movimenti di chi ascolta: annuisce ogni tanto, inclina la testa,
///    si sporge un po' in avanti, respira e sposta leggermente il peso.
/// Quando il giocatore se ne va, torna piano alla posa e alla rotazione iniziali.
/// Mettilo sulla radice dell'NPC (NPC_funghi).
/// </summary>
public class FungaioloAscolta : MonoBehaviour
{
    [Header("Giocatore")]
    [Tooltip("Se vuoto lo cerca da solo (Tag 'Player' o il playercontroller della camera)")]
    public Transform player;
    [Tooltip("Entro questa distanza si gira e ascolta")]
    public float raggio = 4f;

    [Header("Rotazione verso il giocatore")]
    public float velocitaRotazione = 4f;
    [Tooltip("Correzione se il modello guarda di lato o indietro (es. 90, -90, 180)")]
    public float orientamentoModello = 0f;
    [Tooltip("Quando te ne vai torna a guardare dove guardava all'inizio")]
    public bool tornaAllaRotazioneIniziale = true;

    [Header("Testa: annuisce")]
    public float angoloAnnuire = 9f;
    [Tooltip("Ogni quanti secondi annuisce (min-max, scelto a caso)")]
    public Vector2 pausaTraAnnuiti = new Vector2(1.5f, 4f);
    [Tooltip("Asse locale della testa per annuire. Se si muove storta prova (0,0,1) o (-1,0,0)")]
    public Vector3 asseAnnuire = new Vector3(1, 0, 0);

    [Header("Testa: inclinazione curiosa")]
    public float angoloInclinazione = 7f;
    [Tooltip("Asse locale della testa per inclinarla di lato. Se sbaglia prova (1,0,0)")]
    public Vector3 asseInclinazione = new Vector3(0, 0, 1);

    [Header("Corpo")]
    [Tooltip("Quanto si sporge in avanti verso di te mentre ascolta")]
    public float angoloSporgersi = 5f;
    [Tooltip("Asse locale del busto per sporgersi in avanti. Se va all'indietro metti (-1,0,0)")]
    public Vector3 asseSporgersi = new Vector3(1, 0, 0);
    public float angoloRespiro = 1.5f;
    [Tooltip("Spostamento lento del peso da un piede all'altro")]
    public float angoloOndeggiamentoPeso = 2f;
    [Tooltip("Asse locale del busto per lo spostamento di peso laterale")]
    public Vector3 asseOndeggiamento = new Vector3(0, 0, 1);

    [Header("Braccia")]
    [Tooltip("Piccolo movimento delle braccia col respiro (0 = ferme)")]
    public float angoloBraccia = 2f;
    public Vector3 asseBraccia = new Vector3(1, 0, 0);

    [Header("Ossa (trovate in automatico se vuote)")]
    public Transform testa;
    public Transform busto;
    public Transform braccioSx;
    public Transform braccioDx;

    public bool StaAscoltando { get; private set; }

    Quaternion _rotIniziale;
    Quaternion _testaRest, _bustoRest, _bracSxRest, _bracDxRest;
    float _blend;                 // 0 = posa normale, 1 = ascolta
    float _prossimoAnnuire;
    float _inizioAnnuire = -10f;
    const float DurataAnnuire = 0.7f;

    void Awake()
    {
        _rotIniziale = transform.rotation;

        testa = testa ? testa : TrovaOsso("Head");
        busto = busto ? busto : TrovaOsso("Spine00");
        braccioSx = braccioSx ? braccioSx : TrovaOsso("Arm_L");
        braccioDx = braccioDx ? braccioDx : TrovaOsso("Arm_R");

        if (testa) _testaRest = testa.localRotation;
        if (busto) _bustoRest = busto.localRotation;
        if (braccioSx) _bracSxRest = braccioSx.localRotation;
        if (braccioDx) _bracDxRest = braccioDx.localRotation;

        // Un componente Animation vuoto sovrascrive le ossa e dà l'avviso "Default clip could not be found"
        var legacy = GetComponent<Animation>();
        if (legacy != null && legacy.clip == null) legacy.enabled = false;

        _prossimoAnnuire = Time.time + Random.Range(pausaTraAnnuiti.x, pausaTraAnnuiti.y);
    }

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null && Camera.main != null)
        {
            var pc = Camera.main.GetComponentInParent<playercontroller>();
            if (pc != null) player = pc.transform;
        }
        if (player == null)
            Debug.LogWarning("[FungaioloAscolta] Giocatore non trovato: trascinalo nel campo 'Player'.", this);
    }

    void Update()
    {
        if (player == null) return;

        Vector3 versoPlayer = player.position - transform.position;
        versoPlayer.y = 0f;
        StaAscoltando = versoPlayer.magnitude <= raggio;

        // Si gira (resta fermo dov'è, ruota solo su Y)
        Quaternion target = _rotIniziale;
        if (StaAscoltando && versoPlayer.sqrMagnitude > 0.01f)
            target = Quaternion.LookRotation(versoPlayer.normalized) * Quaternion.Euler(0f, orientamentoModello, 0f);

        if (StaAscoltando || tornaAllaRotazioneIniziale)
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * velocitaRotazione);

        // Entra/esce piano dalla posa di ascolto
        _blend = Mathf.MoveTowards(_blend, StaAscoltando ? 1f : 0f, Time.deltaTime * 2f);

        // Programma il prossimo "sì, sì" con la testa
        if (StaAscoltando && Time.time >= _prossimoAnnuire)
        {
            _inizioAnnuire = Time.time;
            _prossimoAnnuire = Time.time + DurataAnnuire + Random.Range(pausaTraAnnuiti.x, pausaTraAnnuiti.y);
        }
    }

    // LateUpdate: applicato dopo eventuali Animator, così il movimento si vede
    void LateUpdate()
    {
        float t = Time.time;

        // Respiro: sempre presente, anche quando non ascolta
        float respiro = Mathf.Sin(t * 1.6f);

        // ---- Busto: respira, si sporge verso di te, sposta il peso ----
        if (busto)
        {
            float sporgersi = angoloSporgersi * _blend;
            float peso = Mathf.Sin(t * 0.45f) * angoloOndeggiamentoPeso * _blend;
            busto.localRotation = _bustoRest
                * Quaternion.AngleAxis(sporgersi + respiro * angoloRespiro, Asse(asseSporgersi, Vector3.right))
                * Quaternion.AngleAxis(peso, Asse(asseOndeggiamento, Vector3.forward));
        }

        // ---- Testa: annuisce a scatti brevi + inclinazione curiosa ----
        if (testa)
        {
            float annuire = 0f;
            float dt = t - _inizioAnnuire;
            if (dt >= 0f && dt <= DurataAnnuire)
            {
                // due piccoli "sì" che si smorzano
                float fase = dt / DurataAnnuire;
                annuire = Mathf.Sin(fase * Mathf.PI * 4f) * (1f - fase) * angoloAnnuire;
            }
            // leggerissimo movimento continuo, come chi segue il discorso
            annuire += Mathf.Sin(t * 0.9f) * 1.2f;

            float incl = (Mathf.PerlinNoise(t * 0.25f, 3.7f) - 0.3f) * 2f * angoloInclinazione;

            testa.localRotation = _testaRest
                * Quaternion.AngleAxis(annuire * _blend, Asse(asseAnnuire, Vector3.right))
                * Quaternion.AngleAxis(incl * _blend, Asse(asseInclinazione, Vector3.forward));
        }

        // ---- Braccia: quasi ferme, si muovono appena col respiro ----
        Vector3 aB = Asse(asseBraccia, Vector3.right);
        if (braccioSx) braccioSx.localRotation = _bracSxRest * Quaternion.AngleAxis(respiro * angoloBraccia, aB);
        if (braccioDx) braccioDx.localRotation = _bracDxRest * Quaternion.AngleAxis(-respiro * angoloBraccia, aB);
    }

    static Vector3 Asse(Vector3 a, Vector3 fallback) => a.sqrMagnitude > 0.0001f ? a.normalized : fallback;

    /// Prende l'osso usato davvero dalla mesh (NPC_funghi ha due Main_Rig annidati).
    Transform TrovaOsso(string nome)
    {
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var b in smr.bones)
                if (b != null && b.name == nome) return b;

        Transform best = null; int bestDepth = -1;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != nome) continue;
            int d = 0; for (var p = t; p != null; p = p.parent) d++;
            if (d > bestDepth) { bestDepth = d; best = t; }
        }
        if (best == null) Debug.LogWarning($"[FungaioloAscolta] Osso '{nome}' non trovato: assegnalo nell'Inspector.", this);
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, raggio);
    }
}
