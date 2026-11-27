using UnityEngine;
using UnityEngine.InputSystem;

public class VitellinoRaccoglibile : MonoBehaviour
{
    [Header("Riferimenti")]
    public Transform player;
    public Malgaro malgaro;

    [Header("Interazione")]
    public KeyCode tastoChiama = KeyCode.E;
    public KeyCode tastoConsegna = KeyCode.P;
    public float distanzaInterazione = 3.5f;
    [Tooltip("Se spuntato, inizia a seguirti subito appena compare senza dover premere E.")]
    public bool seguiSubitoAllaComparsa = false;
    public float distanzaConsegnaMalgaro = 5f;

    [Header("Movimento Inseguimento")]
    public float velocitaCamminata = 4.2f;
    public float velocitaCorsa = 6.5f;
    [Tooltip("Distanza a cui si ferma dietro al player.")]
    public float distanzaStop = 2.2f;
    [Tooltip("Oltre questa distanza inizia a correre per raggiungerti.")]
    public float distanzaCorsa = 5.5f;
    public float velocitaRotazione = 8f;
    [Tooltip("Correzione rotazione Y se il modello guarda di lato o indietro (es. 90, -90, 180).")]
    public float orientamentoModello = 0f;

    [Header("Animazione Passo Procedurale")]
    public float altezzaSaltelloPasso = 0.06f;
    public float frequenzaPasso = 10f;

    [HideInInspector] public bool consegnato = false;

    private bool materializzato = false;
    private bool staSeguendo = false;
    private bool vicinoAlMalgaro = false;
    private float timerPasso = 0f;
    private playerinputcontroller inputController;

    private void Awake()
    {
        // Assicura che all'avvio non risulti già preso o consegnato
        materializzato = false;
        staSeguendo = false;
        consegnato = false;
        vicinoAlMalgaro = false;
    }

    private void Start()
    {
        if (player == null && Camera.main != null)
        {
            var pc = Camera.main.GetComponentInParent<playercontroller>();
            if (pc != null) player = pc.transform;
        }

        if (player != null)
            inputController = player.GetComponent<playerinputcontroller>();

        if (malgaro == null)
            malgaro = FindAnyObjectByType<Malgaro>();
    }

    /// <summary>
    /// Fa materializzare il vitellino davanti al giocatore quando vince il memory.
    /// </summary>
    public void MaterializzaDavantiAlPlayer(Transform playerTransform = null)
    {
        if (playerTransform != null) player = playerTransform;
        if (player == null && Camera.main != null)
        {
            var pc = Camera.main.GetComponentInParent<playercontroller>();
            if (pc != null) player = pc.transform;
        }

        gameObject.SetActive(true);
        materializzato = true;
        staSeguendo = seguiSubitoAllaComparsa;
        consegnato = false;
        vicinoAlMalgaro = false;
        transform.SetParent(null);

        if (player != null)
        {
            Vector3 forwardPiatto = new Vector3(player.forward.x, 0f, player.forward.z).normalized;
            if (forwardPiatto.sqrMagnitude < 0.01f) forwardPiatto = Vector3.forward;

            Vector3 spawnPos = player.position + forwardPiatto * 2.2f;
            spawnPos.y = CalcolaAltezzaTerreno(spawnPos);

            transform.position = spawnPos;

            Vector3 dirVersoPlayer = (player.position - transform.position);
            dirVersoPlayer.y = 0f;
            if (dirVersoPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dirVersoPlayer) * Quaternion.Euler(0f, orientamentoModello, 0f);
            }
        }
    }

    private void Update()
    {
        // Non fa nulla finché il vitellino non è comparso dopo il minigioco o se è già stato consegnato
        if (!materializzato || consegnato || player == null) return;

        if (!staSeguendo)
        {
            // Aspetta che il player lo chiami premendo E
            float dist = DistanzaOrizzontale(transform.position, player.position);
            if (dist <= distanzaInterazione && TastoChiamaPremuto())
            {
                staSeguendo = true;
            }
            return;
        }

        // 1. Controlla SOLO se il player è entro 5 metri orizzontali dal Malgaro (dopo aver preso il vitellino)
        vicinoAlMalgaro = false;
        if (malgaro != null)
        {
            float distPlayerMalgaro = DistanzaOrizzontale(player.position, malgaro.transform.position);

            if (distPlayerMalgaro <= distanzaConsegnaMalgaro)
            {
                vicinoAlMalgaro = true;
                if (TastoConsegnaPremuto())
                {
                    ConsegnaAlMalgaro();
                    return;
                }
            }
        }

        // 2. Segue il player sul terreno
        Vector3 versoPlayer = player.position - transform.position;
        versoPlayer.y = 0f;
        float distanza = versoPlayer.magnitude;

        if (distanza > distanzaStop)
        {
            Vector3 direzione = versoPlayer.normalized;

            Quaternion targetRot = Quaternion.LookRotation(direzione) * Quaternion.Euler(0f, orientamentoModello, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * velocitaRotazione);

            float velocitaAttuale = distanza > distanzaCorsa ? velocitaCorsa : velocitaCamminata;
            Vector3 nuovaPos = transform.position + direzione * velocitaAttuale * Time.deltaTime;

            float altezzaSuolo = CalcolaAltezzaTerreno(nuovaPos);
            timerPasso += Time.deltaTime * frequenzaPasso * (velocitaAttuale / velocitaCamminata);
            float hop = Mathf.Abs(Mathf.Sin(timerPasso)) * altezzaSaltelloPasso;

            nuovaPos.y = altezzaSuolo + hop;
            transform.position = nuovaPos;
        }
        else
        {
            timerPasso = 0f;
            Vector3 posTerra = transform.position;
            posTerra.y = Mathf.Lerp(posTerra.y, CalcolaAltezzaTerreno(posTerra), Time.deltaTime * 10f);
            transform.position = posTerra;

            if (versoPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(versoPlayer.normalized) * Quaternion.Euler(0f, orientamentoModello, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * (velocitaRotazione * 0.5f));
            }
        }
    }

    private float DistanzaOrizzontale(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private float CalcolaAltezzaTerreno(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f))
        {
            return hit.point.y;
        }
        return pos.y;
    }

    private void ConsegnaAlMalgaro()
    {
        staSeguendo = false;
        consegnato = true;
        vicinoAlMalgaro = false;

        // Lo posiziona accanto al malgaro appoggiato a terra
        Vector3 posFinale = malgaro.transform.position + malgaro.transform.right * 1.4f;
        posFinale.y = CalcolaAltezzaTerreno(posFinale);
        transform.position = posFinale;

        Vector3 versoMalgaro = malgaro.transform.position - transform.position;
        versoMalgaro.y = 0f;
        if (versoMalgaro.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(versoMalgaro) * Quaternion.Euler(0f, orientamentoModello, 0f);
        }
    }

    private bool TastoChiamaPremuto()
    {
        bool actionPressed = inputController != null && inputController.ConsumeInteractRequest();
        bool keyPressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        return actionPressed || keyPressed;
    }

    private bool TastoConsegnaPremuto()
    {
        if (Keyboard.current == null) return false;
        string keyName = tastoConsegna.ToString().ToLower();
        var keyControl = Keyboard.current.FindKeyOnCurrentKeyboardLayout(keyName);
        return keyControl != null && keyControl.wasPressedThisFrame;
    }

    private void OnGUI()
    {
        // Mostra le scritte SOLO se il vitellino si è materializzato e non è ancora stato consegnato
        if (!materializzato || consegnato || player == null) return;

        if (!staSeguendo)
        {
            float dist = DistanzaOrizzontale(transform.position, player.position);
            if (dist <= distanzaInterazione)
            {
                GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                s.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40),
                          "[" + tastoChiama + "] Chiama il vitellino", s);
            }
        }
        else
        {
            if (vicinoAlMalgaro)
            {
                GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                s.normal.textColor = new Color(1f, 0.9f, 0.4f);
                GUI.Label(new Rect(0, Screen.height * 0.70f, Screen.width, 40),
                          "[" + tastoConsegna + "] Restituisci il vitellino al malgaro", s);
            }

            GUIStyle o = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            o.normal.textColor = new Color(0.5f, 1f, 0.5f);
            GUI.Label(new Rect(20, 50, 700, 30), "Il vitellino ti sta seguendo! Riportalo dal malgaro.", o);
        }
    }
}