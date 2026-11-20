using UnityEngine;
using UnityEngine.InputSystem;

public class Malgaro : MonoBehaviour
{
    public playercontroller player;
    public PuntoMinigioco punto;     // trascina qui l'oggetto con PuntoMinigioco
    public string nome = "Il malgaro";
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 3f;
    [SerializeField] float orientamentoModello = 0f;

    [TextArea(2, 4)]
    public string[] dialogoIniziale = new string[]
    {
        "Ah, un escursionista! Meno male, ho bisogno di una mano.",
        "Guarda questi pascoli: ai tempi di mio padre erano verdi fino al torrente. Adesso è tutto giallo e secco.",
        "Le mucche si sono sparpagliate in cerca d'erba, fin sopra le rocce.",
        "E un vitellino è rimasto indietro, troppo debole per seguire le altre.",
        "Lo sento muggire da qualche parte, tra le rocce e i cespugli lassù. Puoi cercarlo?"
    };

    [TextArea(2, 4)]
    public string[] dialogoPromemoria = new string[]
    {
        "Il vitellino è tra le rocce e i cespugli, in alto. Sbrigati: con questo caldo non resiste molto."
    };

    [TextArea(2, 4)]
    public string[] dialogoFinale = new string[]
    {
        "Il mio vitellino! Grazie, ragazzo.",
        "Una volta il caldo così non c'era nemmeno a luglio..."
    };

    string[] righeAttuali;
    int indice;
    bool parlando;
    bool haParlato;
    System.Action allaFine;
    playerinputcontroller inputController;

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.GetComponentInParent<playercontroller>();

        if (player != null)
            inputController = player.GetComponent<playerinputcontroller>();

        if (punto == null)
        {
            PuntoMinigioco[] punti =
                FindObjectsByType<PuntoMinigioco>(FindObjectsInactive.Exclude);
            float distanzaMinima = float.MaxValue;

            foreach (PuntoMinigioco candidato in punti)
            {
                if (candidato.minigioco == null)
                    continue;

                float distanza = (candidato.transform.position - transform.position).sqrMagnitude;
                if (distanza < distanzaMinima)
                {
                    distanzaMinima = distanza;
                    punto = candidato;
                }
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        if (parlando)
        {
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion rotazioneVersoPlayer =
                    Quaternion.LookRotation(dir) *
                    Quaternion.Euler(0f, orientamentoModello, 0f);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, rotazioneVersoPlayer, Time.deltaTime * 4f);
            }

            if (InterazionePremuta() ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                Avanti();
            return;
        }

        if (InRange() && InterazionePremuta()) Inizia();
    }

    bool InterazionePremuta()
    {
        bool actionPressed = inputController != null && inputController.ConsumeInteractRequest();
        bool keyboardPressed = Keyboard.current != null &&
                               Keyboard.current.eKey.wasPressedThisFrame;
        return actionPressed || keyboardPressed;
    }

    void Inizia()
    {
        if (punto != null && punto.completato)
            Dialogo(dialogoFinale, null);
        else if (!haParlato)
            Dialogo(dialogoIniziale, () => { haParlato = true; if (punto != null) punto.abilitato = true; });
        else
            Dialogo(dialogoPromemoria, null);
    }

    void Dialogo(string[] righe, System.Action fine)
    {
        if (righe == null || righe.Length == 0) return;
        righeAttuali = righe;
        indice = 0;
        allaFine = fine;
        parlando = true;
        player.canMove = false;
    }

    void Avanti()
    {
        indice++;
        if (indice >= righeAttuali.Length)
        {
            parlando = false;
            player.canMove = true;
            if (allaFine != null) allaFine();
            allaFine = null;
        }
    }

    bool InRange()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 toNpc = (transform.position + Vector3.up * 0.8f) - cam.transform.position;
        if (toNpc.magnitude > interactDistance) return false;
        return Vector3.Dot(cam.transform.forward, toNpc.normalized) > 0.5f;
    }

    void OnGUI()
    {
        if (player == null) return;

        if (parlando)
        {
            float w = Screen.width * 0.7f, h = 140f;
            float x = (Screen.width - w) / 2f, y = Screen.height - h - 40f;
            GUI.Box(new Rect(x, y, w, h), "");

            GUIStyle n = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            n.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(x + 20, y + 10, w - 40, 30), nome, n);

            GUIStyle t = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            t.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 20, y + 45, w - 40, h - 70), righeAttuali[indice], t);

            GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerRight };
            hint.normal.textColor = new Color(1, 1, 1, 0.6f);
            GUI.Label(new Rect(x, y, w - 15, h - 8), "[" + interactKey + "] continua", hint);
        }
        else if (InRange())
        {
            GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40),
                      "[" + interactKey + "] Parla con " + nome, s);
        }

        if (haParlato && punto != null && !punto.completato)
        {
            GUIStyle o = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            o.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(20, 20, 700, 30), "Obiettivo: trova il vitellino tra le rocce", o);
        }
    }
}