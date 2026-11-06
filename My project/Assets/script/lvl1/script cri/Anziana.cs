using UnityEngine;

public class Anziana : MonoBehaviour
{
    public PlayerFPS player;
    public PuntoTorrente punto;
    public string nome = "La signora della baita";
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 3f;

    [TextArea(2, 4)]
    public string[] dialogoIniziale = new string[]
    {
        "Buongiorno, ragazzo. Non sei di queste parti, vero?",
        "Vedi quel rigagnolo laggiù? Quando ero giovane era un torrente vero, largo e rumoroso. Scendeva dal ghiacciaio e non finiva mai.",
        "Adesso il ghiacciaio si è ritirato, in alto, e l'acqua non arriva quasi più. Non ne ho abbastanza nemmeno per cucinare.",
        "Se riesci a riempirmi questo secchio, te ne sarò grata. Ma attento alle rocce: scivolano, e l'acqua è poca e fangosa."
    };

    [TextArea(2, 4)]
    public string[] dialogoPromemoria = new string[]
    {
        "Il torrente è lì sotto. Scendi piano e non prendere il fango: quello non si beve."
    };

    [TextArea(2, 4)]
    public string[] dialogoFinale = new string[]
    {
        "Acqua pulita... non ne vedevo così da tanto tempo.",
        "Grazie, ragazzo. Una volta bastava allungare una mano per berne quanta ne volevi."
    };

    string[] righeAttuali;
    int indice;
    bool parlando;
    bool haParlato;
    System.Action allaFine;

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.GetComponentInParent<PlayerFPS>();
    }

    void Update()
    {
        if (player == null) return;

        if (parlando)
        {
            Vector3 dir = player.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);

            if (Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0)) Avanti();
            return;
        }

        if (InRange() && Input.GetKeyDown(interactKey)) Inizia();
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
            GUI.Label(new Rect(20, 20, 700, 30), "Obiettivo: raccogli l'acqua dal torrente", o);
        }
    }
}