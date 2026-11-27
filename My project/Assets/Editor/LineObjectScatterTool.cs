using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LineObjectScatterTool : EditorWindow
{
    [SerializeField] private GameObject prefabDaDistribuire;
    [SerializeField] private List<Vector3> puntiLinea = new List<Vector3>();
    [SerializeField] private bool disegnaLinea;
    [SerializeField] private float distanzaTraOggetti = 2f;
    [SerializeField] private float variazioneRotazione = 0f;
    [SerializeField] private float variazioneScala = 0f;
    [SerializeField] private int seme = 1234;

    [MenuItem("Tools/Distribuzione/Metti oggetti lungo una linea")]
    private static void ApriTool()
    {
        GetWindow<LineObjectScatterTool>("Oggetti lungo linea");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DisegnaLineaNellaScena;
        SceneView.duringSceneGui += DisegnaLineaNellaScena;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DisegnaLineaNellaScena;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Distribuisci oggetti lungo una linea", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scegli un prefab, disegna una linea nella Scene View e genera copie lungo il percorso. " +
            "Il tool funziona con staccionate, pali, lampioni o qualsiasi altro prefab.",
            MessageType.Info);

        prefabDaDistribuire = (GameObject)EditorGUILayout.ObjectField(
            "Prefab da distribuire", prefabDaDistribuire, typeof(GameObject), false);
        distanzaTraOggetti = Mathf.Max(0.05f,
            EditorGUILayout.FloatField("Distanza tra oggetti", distanzaTraOggetti));
        variazioneRotazione = Mathf.Clamp(
            EditorGUILayout.FloatField("Variazione rotazione", variazioneRotazione), 0f, 45f);
        variazioneScala = Mathf.Clamp(
            EditorGUILayout.FloatField("Variazione scala", variazioneScala), 0f, 0.8f);
        seme = EditorGUILayout.IntField("Seme", seme);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Linea", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Punti inseriti: " + puntiLinea.Count);

        if (GUILayout.Button(disegnaLinea ? "Termina linea" : "Disegna linea nella Scene View"))
        {
            disegnaLinea = !disegnaLinea;
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Togli ultimo punto") && puntiLinea.Count > 0)
        {
            Undo.RecordObject(this, "Modifica linea oggetti");
            puntiLinea.RemoveAt(puntiLinea.Count - 1);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Svuota linea"))
        {
            Undo.RecordObject(this, "Svuota linea oggetti");
            puntiLinea.Clear();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("GENERA OGGETTI SULLA LINEA", GUILayout.Height(35f)))
            Genera();
        GUI.enabled = true;
    }

    private void DisegnaLineaNellaScena(SceneView sceneView)
    {
        Handles.color = new Color(1f, 0.65f, 0.1f, 1f);
        for (int i = 0; i < puntiLinea.Count; i++)
        {
            Handles.SphereHandleCap(0, puntiLinea[i] + Vector3.up * 0.08f,
                Quaternion.identity, 0.2f, EventType.Repaint);
            if (i > 0)
                Handles.DrawAAPolyLine(4f, puntiLinea[i - 1], puntiLinea[i]);
        }

        if (!disegnaLinea)
            return;

        Event evento = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (evento.type != EventType.MouseDown || evento.button != 0 || evento.alt)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(evento.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            Undo.RecordObject(this, "Aggiungi punto linea oggetti");
            puntiLinea.Add(hit.point);
            evento.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void Genera()
    {
        if (prefabDaDistribuire == null || !EditorUtility.IsPersistent(prefabDaDistribuire))
        {
            Debug.LogWarning("Assegna un prefab dal pannello Project.");
            return;
        }

        if (puntiLinea.Count < 2)
        {
            Debug.LogWarning("Disegna una linea con almeno due punti.");
            return;
        }

        Scene scena = SceneManager.GetActiveScene();
        GameObject radice = new GameObject("Oggetti lungo linea - " + prefabDaDistribuire.name);
        SceneManager.MoveGameObjectToScene(radice, scena);
        Undo.RegisterCreatedObjectUndo(radice, "Genera oggetti lungo linea");

        Random.InitState(seme);
        int numero = 0;
        for (int segmento = 0; segmento < puntiLinea.Count - 1; segmento++)
        {
            Vector3 inizio = puntiLinea[segmento];
            Vector3 fine = puntiLinea[segmento + 1];
            Vector3 direzione = fine - inizio;
            float lunghezza = new Vector2(direzione.x, direzione.z).magnitude;
            if (lunghezza < 0.01f)
                continue;

            Vector3 direzioneOrizzontale = new Vector3(direzione.x, 0f, direzione.z).normalized;
            int copie = Mathf.FloorToInt(lunghezza / distanzaTraOggetti);
            for (int i = 0; i <= copie; i++)
            {
                float distanza = Mathf.Min(i * distanzaTraOggetti, lunghezza);
                Vector3 posizione = inizio + direzioneOrizzontale * distanza;
                posizione.y = Mathf.Lerp(inizio.y, fine.y, distanza / lunghezza);

                GameObject copia = PrefabUtility.InstantiatePrefab(prefabDaDistribuire, scena) as GameObject;
                if (copia == null)
                    continue;

                copia.name = prefabDaDistribuire.name + " - linea";
                copia.transform.SetParent(radice.transform, true);
                copia.transform.position = posizione;

                float angoloLinea = Mathf.Atan2(direzioneOrizzontale.x, direzioneOrizzontale.z) * Mathf.Rad2Deg;
                float angoloCasuale = Random.Range(-variazioneRotazione, variazioneRotazione);
                copia.transform.rotation = Quaternion.Euler(0f, angoloLinea + angoloCasuale, 0f);

                float scala = 1f + Random.Range(-variazioneScala, variazioneScala);
                copia.transform.localScale = prefabDaDistribuire.transform.localScale * scala;
                Undo.RegisterCreatedObjectUndo(copia, "Aggiungi oggetto lungo linea");
                numero++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scena);
        Selection.activeGameObject = radice;
        Debug.Log("Oggetti generati lungo la linea: " + numero + ". Ctrl+Z annulla il gruppo.");
    }
}
