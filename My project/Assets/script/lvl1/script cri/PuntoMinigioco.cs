using UnityEngine;
using UnityEngine.InputSystem;

public class PuntoMinigioco : MonoBehaviour
{
    public MemoryMinigame minigioco;
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 3.5f;
    public string testo = "Cerca il vitellino tra le rocce";

    [HideInInspector] public bool abilitato = false;   // lo attiva il malgaro
    [HideInInspector] public bool completato = false;

    void Start()
    {
        if (minigioco == null)
        {
            minigioco = GetComponentInChildren<MemoryMinigame>(true);
            if (minigioco == null)
                minigioco = GetComponentInParent<MemoryMinigame>();
        }

        if (minigioco != null)
            minigioco.onVittoria.AddListener(() => completato = true);
    }

    void Update()
    {
        if (!abilitato || completato || minigioco == null || minigioco.InCorso) return;

        if (InRange() && Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
            minigioco.Avvia();
    }

    bool InRange()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 toPoint = (transform.position + Vector3.up * 0.5f) - cam.transform.position;
        if (toPoint.magnitude > interactDistance) return false;
        return Vector3.Dot(cam.transform.forward, toPoint.normalized) > 0.4f;
    }

    void OnGUI()
    {
        if (!abilitato || completato || minigioco == null || minigioco.InCorso || !InRange()) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 22;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 40),
                  "[" + interactKey + "] " + testo, style);
    }
}