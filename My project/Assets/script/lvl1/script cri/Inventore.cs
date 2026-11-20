using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class Inventore : MonoBehaviour
{
    [Header("Riferimenti")]
    public playercontroller player;
    public RaccoltaLegnetti minigioco;
    public GameObject rifugioFinale;   // catasta 3D nascosta all'inizio (facoltativa)

    [Header("Interazione")]
    public string nome = "L'inventore";
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 3f;

    [Header("Dialoghi")]
    [TextArea(2, 4)]
    public string[] dialogoIniziale = new string[]
    {
        "Ehi, tu! Hai due mani libere? Perfetto, mi serve una mano.",
        "Mi chiamano l'inventore del bosco, ma da bambino ero solo uno che passava i pomeriggi qui a contare le specie: scoiattoli, picchi, ghiri... ogni settimana ne scoprivo una nuova.",
        "Poi gli alberi hanno cominciato a seccarsi o a sparire, per il caldo e per i tagli. E gli animali se ne sono andati più in alto, più lontano.",
        "Così ho inventato lo scuotirami: fa cadere i rami secchi, quelli che agli alberi non servono più. Con quelli costruisco mini-rifugi: cataste di legno che fanno ombra e tana.",
        "Tu prendi i legnetti con la cesta. Attento alle pigne, però: sono dure e non servono. Con dodici legnetti il rifugio sta in piedi."
    };

    [TextArea(2, 4)]
    public string[] dialogoPromemoria = new string[]
    {
        "Muovi la cesta con le frecce, prendi i legnetti e lascia perdere le pigne. Dodici e il rifugio è pronto."
    };

    [TextArea(2, 4)]
    public string[] dialogoFinale = new string[]
    {
        "Dodici legnetti! Guarda, il rifugio sta in piedi.",
        "Qui sotto troveranno ombra nelle ore calde e una tana per l'inverno: ricci, ghiri, magari qualche uccello.",
        "Non basta un rifugio per salvare un bosco, ma ogni catasta è un posto dove la fauna può tornare.",
        "Sai, mi ricordi qualcuno: un escursionista con uno zaino enorme che incontrai da bambino. Mi disse di non smettere mai di guardare. Aveva ragione."
    };

    [TextArea(2, 4)]
    public string[] dialogoPostMissione = new string[]
    {
        "Grazie ancora. Ora vado a cercare altri rami: i rifugi non si costruiscono da soli."
    };

    [Header("Eventi")]
    public UnityEvent onMissioneIniziata;
    public UnityEvent onMissioneCompletata;

    enum Stato { NonIniziata, InCorso, Completata }
    Stato stato = Stato.NonIniziata;

    string[] righeAttuali;
    int indice;
    bool parlando;
    System.Action allaFine;
    playerinputcontroller inputController;

    void Start()
    {
        if (rifugioFinale != null) rifugioFinale.SetActive(false);
        if (minigioco != null) minigioco.onVittoria.AddListener(AlMinigiocoVinto);

        if (player == null && Camera.main != null)
            player = Camera.main.GetComponentInParent<playercontroller>();

        if (player != null)
            inputController = player.GetComponent<playerinputcontroller>();
    }

    void AlMinigiocoVinto()
    {
        if (rifugioFinale != null) rifugioFinale.SetActive(true);

        Dialogo(dialogoFinale, () =>
        {
            stato = Stato.Completata;
            if (onMissioneCompletata != null) onMissioneCompletata.Invoke();
        });
    }

    void Update()
    {
        if (player == null) return;
        if (minigioco != null && minigioco.InCorso) return;

        if (parlando)
        {
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);

            if (InterazionePremuta() || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                Avanti();
            return;
        }

        if (InRange() && InterazionePremuta()) Interagisci();
    }

    bool InterazionePremuta()
    {
        bool actionPressed = inputController != null && inputController.ConsumeInteractRequest();
        bool keyboardPressed = Keyboard.current != null &&
                               Keyboard.current.eKey.wasPressedThisFrame;
        return actionPressed || keyboardPressed;
    }

    void Interagisci()
    {
        switch (stato)
        {
            case Stato.NonIniziata:
                Dialogo(dialogoIniziale, () =>
                {
                    stato = Stato.InCorso;
                    if (onMissioneIniziata != null) onMissioneIniziata.Invoke();
                    if (minigioco != null) minigioco.Avvia();
                });
                break;

            case Stato.InCorso:
                Dialogo(dialogoPromemoria, () => { if (minigioco != null) minigioco.Avvia(); });
                break;

            case Stato.Completata:
                Dialogo(dialogoPostMissione, null);
                break;
        }
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

        Vector3 verso = (transform.position + Vector3.up * 0.8f) - cam.transform.position;
        if (verso.magnitude > interactDistance) return false;
        return Vector3.Dot(cam.transform.forward, verso.normalized) > 0.5f;
    }

    void OnGUI()
    {
        if (player == null) return;
        if (minigioco != null && minigioco.InCorso) return;

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

        if (stato == Stato.InCorso)
        {
            GUIStyle o = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            o.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(20, 20, 700, 30), "Obiettivo: aiuta l'inventore a raccogliere i legnetti", o);
        }
    }
}