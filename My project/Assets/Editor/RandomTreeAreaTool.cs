using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RandomTreeAreaTool : EditorWindow
{
    private const int AlberiPerTentativo = 80;
    private const int TentativiMinimi = 40;
    private const int TentativiMassimi = 5000;

    [SerializeField] private List<GameObject> prefabAlberi = new List<GameObject>();
    [SerializeField] private List<Vector3> puntiArea = new List<Vector3>();
    [SerializeField] private bool disegnaArea;
    [SerializeField] private int seme = 12345;

    [MenuItem("Tools/Bosco/Distribuisci alberi casualmente")]
    private static void ApriTool()
    {
        GetWindow<RandomTreeAreaTool>("Alberi casuali");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DisegnaSceneView;
        SceneView.duringSceneGui += DisegnaSceneView;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DisegnaSceneView;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Distribuzione casuale alberi", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Disegna un'area nella Scene View e assegna uno o piu' prefab di alberi. " +
            "Il tool scegliera' casualmente tra i prefab assegnati.",
            MessageType.Info);

        EditorGUILayout.LabelField("Prefab degli alberi", EditorStyles.boldLabel);
        for (int i = 0; i < prefabAlberi.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            prefabAlberi[i] = (GameObject)EditorGUILayout.ObjectField(
                prefabAlberi[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                prefabAlberi.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Aggiungi prefab albero"))
            prefabAlberi.Add(null);

        seme = EditorGUILayout.IntField("Seme casuale", seme);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Punti inseriti: " + puntiArea.Count);

        if (GUILayout.Button(disegnaArea ? "Termina disegno" : "Disegna area nella Scene View"))
        {
            disegnaArea = !disegnaArea;
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Togli ultimo punto") && puntiArea.Count > 0)
        {
            Undo.RecordObject(this, "Modifica area alberi");
            puntiArea.RemoveAt(puntiArea.Count - 1);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Svuota area"))
        {
            Undo.RecordObject(this, "Svuota area alberi");
            puntiArea.Clear();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("GENERA ALBERI NELL'AREA", GUILayout.Height(35f)))
            GeneraAlberi();
        GUI.enabled = true;

        EditorGUILayout.HelpBox(
            "La quantita' viene calcolata automaticamente in base alla superficie. " +
            "Per disegnare e generare serve un Terrain Collider o un altro Collider del terreno attivo.",
            MessageType.None);
    }

    private void DisegnaSceneView(SceneView sceneView)
    {
        Handles.color = new Color(0.2f, 1f, 0.35f, 1f);
        for (int i = 0; i < puntiArea.Count; i++)
        {
            Handles.SphereHandleCap(0, puntiArea[i] + Vector3.up * 0.08f,
                Quaternion.identity, 0.25f, EventType.Repaint);
            Handles.Label(puntiArea[i] + Vector3.up * 0.25f, (i + 1).ToString());
            if (i > 0)
                Handles.DrawAAPolyLine(3f, puntiArea[i - 1], puntiArea[i]);
        }

        if (puntiArea.Count >= 3)
            Handles.DrawAAPolyLine(3f, puntiArea[puntiArea.Count - 1], puntiArea[0]);

        if (!disegnaArea)
            return;

        Event evento = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (evento.type != EventType.MouseDown || evento.button != 0 || evento.alt)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(evento.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            Undo.RecordObject(this, "Aggiungi punto area alberi");
            puntiArea.Add(hit.point);
            evento.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void GeneraAlberi()
    {
        if (prefabAlberi.Count == 0)
        {
            Debug.LogWarning("Aggiungi almeno un prefab nella lista degli alberi.");
            return;
        }

        if (puntiArea.Count < 3)
        {
            Debug.LogWarning("Disegna un'area con almeno tre punti.");
            return;
        }

        List<GameObject> prefabValidi = new List<GameObject>();
        foreach (GameObject prefab in prefabAlberi)
            if (prefab != null && EditorUtility.IsPersistent(prefab))
                prefabValidi.Add(prefab);

        if (prefabValidi.Count == 0)
        {
            Debug.LogWarning("I prefab devono essere asset presenti nel pannello Project.");
            return;
        }

        Scene scena = SceneManager.GetActiveScene();
        if (!scena.IsValid())
            return;

        Bounds areaBounds = CalcolaBoundsArea();
        float areaSuperficie = Mathf.Abs(CalcolaAreaPoligono());
        int quantita = Mathf.Clamp(Mathf.RoundToInt(areaSuperficie / 12f), 1, 200);
        int tentativiMassimi = Mathf.Clamp(quantita * AlberiPerTentativo, TentativiMinimi, TentativiMassimi);
        System.Random random = new System.Random(seme);

        GameObject radice = new GameObject("Alberi casuali generati");
        SceneManager.MoveGameObjectToScene(radice, scena);
        Undo.RegisterCreatedObjectUndo(radice, "Genera alberi casuali");

        int creati = 0;
        for (int tentativo = 0; tentativo < tentativiMassimi && creati < quantita; tentativo++)
        {
            float x = Mathf.Lerp(areaBounds.min.x, areaBounds.max.x, (float)random.NextDouble());
            float z = Mathf.Lerp(areaBounds.min.z, areaBounds.max.z, (float)random.NextDouble());
            if (!PuntoDentroPoligono(new Vector2(x, z)))
                continue;

            Ray ray = new Ray(new Vector3(x, areaBounds.max.y + 100f, z), Vector3.down);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10000f))
                continue;

            GameObject prefab = prefabValidi[random.Next(prefabValidi.Count)];
            GameObject albero = PrefabUtility.InstantiatePrefab(prefab, scena) as GameObject;
            if (albero == null)
                continue;

            albero.name = prefab.name + " - casuale";
            albero.transform.SetParent(radice.transform, true);
            albero.transform.position = hit.point;
            albero.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            float scala = Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());
            albero.transform.localScale = prefab.transform.localScale * scala;
            Undo.RegisterCreatedObjectUndo(albero, "Aggiungi albero casuale");
            creati++;
        }

        EditorSceneManager.MarkSceneDirty(scena);
        Selection.activeGameObject = radice;
        Debug.Log("Alberi generati: " + creati + "/" + quantita + ". Ctrl+Z annulla l'intero gruppo.");
    }

    private Bounds CalcolaBoundsArea()
    {
        Bounds bounds = new Bounds(puntiArea[0], Vector3.zero);
        foreach (Vector3 punto in puntiArea)
            bounds.Encapsulate(punto);
        return bounds;
    }

    private float CalcolaAreaPoligono()
    {
        float area = 0f;
        for (int i = 0, j = puntiArea.Count - 1; i < puntiArea.Count; j = i++)
        {
            Vector2 a = new Vector2(puntiArea[i].x, puntiArea[i].z);
            Vector2 b = new Vector2(puntiArea[j].x, puntiArea[j].z);
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private bool PuntoDentroPoligono(Vector2 punto)
    {
        bool dentro = false;
        for (int i = 0, j = puntiArea.Count - 1; i < puntiArea.Count; j = i++)
        {
            Vector2 a = new Vector2(puntiArea[i].x, puntiArea[i].z);
            Vector2 b = new Vector2(puntiArea[j].x, puntiArea[j].z);
            if ((a.y > punto.y) != (b.y > punto.y) &&
                punto.x < (b.x - a.x) * (punto.y - a.y) / (b.y - a.y) + a.x)
                dentro = !dentro;
        }
        return dentro;
    }
}
