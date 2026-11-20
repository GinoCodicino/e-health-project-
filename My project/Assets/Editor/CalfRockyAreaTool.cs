using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CalfRockyAreaTool : EditorWindow
{
    private const string TargetName = "rocce vitellino";
    private const string GeneratedPrefix = "Sassiera vitellino";

    [SerializeField] private GameObject centro;
    [SerializeField] private List<GameObject> prefabRocce = new List<GameObject>();
    [SerializeField] private List<GameObject> prefabArbusti = new List<GameObject>();
    [SerializeField] private float raggio = 6f;
    [SerializeField] private float rialzo = 0.6f;
    [SerializeField] private int numeroRocce = 24;
    [SerializeField] private int numeroArbusti = 12;
    [SerializeField] private int seme = 8123;

    [MenuItem("Tools/Level 01/Sassiera del vitellino")]
    private static void ApriTool()
    {
        GetWindow<CalfRockyAreaTool>("Sassiera vitellino");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sassiera del vitellino", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Crea una piccola zona rialzata con rocce di varie dimensioni e arbusti attorno a 'rocce vitellino'.",
            MessageType.Info);

        centro = (GameObject)EditorGUILayout.ObjectField("Centro sassiera", centro, typeof(GameObject), true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Trova 'rocce vitellino'"))
            centro = TrovaCentro();
        if (GUILayout.Button("Usa selezione"))
            centro = Selection.activeGameObject;
        EditorGUILayout.EndHorizontal();

        raggio = Mathf.Clamp(EditorGUILayout.FloatField("Raggio area", raggio), 1f, 30f);
        rialzo = Mathf.Clamp(EditorGUILayout.FloatField("Rialzo centrale", rialzo), 0f, 5f);
        numeroRocce = Mathf.Clamp(EditorGUILayout.IntField("Numero rocce", numeroRocce), 0, 200);
        numeroArbusti = Mathf.Clamp(EditorGUILayout.IntField("Numero arbusti", numeroArbusti), 0, 100);
        seme = EditorGUILayout.IntField("Seme disposizione", seme);

        EditorGUILayout.Space();
        DisegnaListaPrefab("Prefab rocce", prefabRocce);
        DisegnaListaPrefab("Prefab arbusti", prefabArbusti);

        EditorGUILayout.Space();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("GENERA SASSIERA", GUILayout.Height(35f)))
            Genera();
        if (GUILayout.Button("RIMUOVI SASSIERE GENERATE"))
            Rimuovi();
        GUI.enabled = true;
    }

    private static void DisegnaListaPrefab(string titolo, List<GameObject> lista)
    {
        EditorGUILayout.LabelField(titolo, EditorStyles.boldLabel);
        for (int i = 0; i < lista.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            lista[i] = (GameObject)EditorGUILayout.ObjectField(lista[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                lista.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Aggiungi prefab", EditorStyles.miniButton))
            lista.Add(null);
    }

    private void Genera()
    {
        if (centro == null)
            centro = TrovaCentro();

        if (centro == null || !centro.scene.IsValid())
        {
            Debug.LogWarning("Seleziona o assegna l'oggetto 'rocce vitellino' nella scena.");
            return;
        }

        Scene scena = centro.scene;
        GameObject radice = new GameObject(GeneratedPrefix);
        SceneManager.MoveGameObjectToScene(radice, scena);
        Undo.RegisterCreatedObjectUndo(radice, "Genera sassiera vitellino");

        System.Random random = new System.Random(seme);
        Vector3 origine = centro.transform.position;

        CreaBaseRialzata(radice.transform, origine);

        List<GameObject> rocceValide = PrefabValidi(prefabRocce);
        List<GameObject> arbustiValidi = PrefabValidi(prefabArbusti);

        for (int i = 0; i < numeroRocce; i++)
        {
            Vector3 posizione = PuntoCasuale(origine, random, 0.35f, 1f);
            float dimensione = Mathf.Lerp(0.35f, 1.5f, (float)random.NextDouble());
            if (rocceValide.Count > 0)
                CreaPrefab(radice.transform, rocceValide[random.Next(rocceValide.Count)], posizione, dimensione, random, "Roccia vitellino");
            else
                CreaRocciaProcedurale(radice.transform, posizione, dimensione, random);
        }

        for (int i = 0; i < numeroArbusti; i++)
        {
            Vector3 posizione = PuntoCasuale(origine, random, 0.55f, 0.98f);
            float dimensione = Mathf.Lerp(0.5f, 1.1f, (float)random.NextDouble());
            if (arbustiValidi.Count > 0)
                CreaPrefab(radice.transform, arbustiValidi[random.Next(arbustiValidi.Count)], posizione, dimensione, random, "Arbusto sassiera");
            else
                CreaArbustoProcedurale(radice.transform, posizione, dimensione, random);
        }

        EditorSceneManager.MarkSceneDirty(scena);
        Selection.activeGameObject = radice;
        Debug.Log("Sassiera del vitellino generata. Ctrl+Z annulla l'intero gruppo.");
    }

    private void CreaBaseRialzata(Transform genitore, Vector3 origine)
    {
        GameObject baseArea = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        baseArea.name = "Rialzo sassiera";
        baseArea.transform.SetParent(genitore, true);
        baseArea.transform.position = origine + Vector3.up * (rialzo * 0.45f);
        baseArea.transform.localScale = new Vector3(raggio * 2f, Mathf.Max(0.05f, rialzo), raggio * 2f);
        ImpostaMateriale(baseArea, new Color(0.24f, 0.30f, 0.16f));
        RimuoviCollider(baseArea);
    }

    private Vector3 PuntoCasuale(Vector3 origine, System.Random random, float minimo, float massimo)
    {
        float angolo = (float)random.NextDouble() * Mathf.PI * 2f;
        float distanzaPunto = Mathf.Lerp(minimo, massimo, Mathf.Sqrt((float)random.NextDouble())) * raggio;
        float rialzoPunto = rialzo * Mathf.Clamp01(1f - distanzaPunto / raggio);
        return origine + new Vector3(Mathf.Cos(angolo) * distanzaPunto, rialzoPunto, Mathf.Sin(angolo) * distanzaPunto);
    }

    private static void CreaPrefab(Transform genitore, GameObject prefab, Vector3 posizione, float dimensione, System.Random random, string nome)
    {
        GameObject copia = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (copia == null)
            return;

        copia.name = nome;
        copia.transform.SetParent(genitore, true);
        copia.transform.position = posizione;
        copia.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        copia.transform.localScale *= dimensione;
        Undo.RegisterCreatedObjectUndo(copia, "Aggiungi elemento sassiera");
    }

    private static void CreaRocciaProcedurale(Transform genitore, Vector3 posizione, float dimensione, System.Random random)
    {
        GameObject roccia = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        roccia.name = "Roccia vitellino";
        roccia.transform.SetParent(genitore, true);
        roccia.transform.position = posizione + Vector3.up * dimensione * 0.35f;
        roccia.transform.rotation = Quaternion.Euler(
            (float)random.NextDouble() * 30f,
            (float)random.NextDouble() * 360f,
            (float)random.NextDouble() * 30f);
        roccia.transform.localScale = new Vector3(
            dimensione * RandomRange(random, 0.8f, 1.4f),
            dimensione * RandomRange(random, 0.45f, 0.9f),
            dimensione * RandomRange(random, 0.7f, 1.3f));
        ImpostaMateriale(roccia, new Color(0.30f, 0.34f, 0.31f));
        RimuoviCollider(roccia);
    }

    private static void CreaArbustoProcedurale(Transform genitore, Vector3 posizione, float dimensione, System.Random random)
    {
        GameObject arbusto = new GameObject("Arbusto sassiera");
        arbusto.transform.SetParent(genitore, true);
        arbusto.transform.position = posizione;
        arbusto.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        arbusto.transform.localScale = Vector3.one * dimensione;

        for (int i = 0; i < 3; i++)
        {
            GameObject foglia = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foglia.name = "Foglia arbusto";
            foglia.transform.SetParent(arbusto.transform, false);
            foglia.transform.localPosition = new Vector3(
                RandomRange(random, -0.3f, 0.3f),
                RandomRange(random, 0.25f, 0.6f),
                RandomRange(random, -0.3f, 0.3f));
            foglia.transform.localScale = Vector3.one * RandomRange(random, 0.35f, 0.65f);
            ImpostaMateriale(foglia, new Color(0.12f, RandomRange(random, 0.45f, 0.75f), 0.16f));
            RimuoviCollider(foglia);
        }
    }

    private static float RandomRange(System.Random random, float minimo, float massimo)
    {
        return Mathf.Lerp(minimo, massimo, (float)random.NextDouble());
    }

    private static List<GameObject> PrefabValidi(List<GameObject> lista)
    {
        List<GameObject> validi = new List<GameObject>();
        foreach (GameObject prefab in lista)
            if (prefab != null && EditorUtility.IsPersistent(prefab))
                validi.Add(prefab);
        return validi;
    }

    private static GameObject TrovaCentro()
    {
        foreach (GameObject oggetto in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (oggetto.scene.IsValid() && oggetto.name.IndexOf(TargetName, StringComparison.OrdinalIgnoreCase) >= 0)
                return oggetto;
        }
        return null;
    }

    private static void ImpostaMateriale(GameObject oggetto, Color colore)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        Material materiale = new Material(shader);
        materiale.color = colore;
        if (materiale.HasProperty("_BaseColor")) materiale.SetColor("_BaseColor", colore);
        Renderer renderer = oggetto.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = materiale;
    }

    private static void RimuoviCollider(GameObject oggetto)
    {
        Collider collider = oggetto.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);
    }

    private static void Rimuovi()
    {
        foreach (GameObject oggetto in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!oggetto.scene.IsValid() || oggetto.name != GeneratedPrefix)
                continue;
            Undo.DestroyObjectImmediate(oggetto);
            return;
        }
    }
}
