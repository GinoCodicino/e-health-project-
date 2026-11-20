using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HousePerimeterDecorationTool : EditorWindow
{
    private int arbustiPerCasa = 8;
    private int fioriPerCasa = 16;
    private float distanza = 1f;
    private int seme = 1234;
    [SerializeField] private List<GameObject> caseManuali = new List<GameObject>();
    [SerializeField] private List<GameObject> prefabArbusti = new List<GameObject>();
    [SerializeField] private List<GameObject> prefabFiori = new List<GameObject>();

    private static readonly Color[] ColoriFiori =
    {
        new Color(1f, 0.15f, 0.25f),
        new Color(1f, 0.75f, 0.05f),
        new Color(0.55f, 0.2f, 1f),
        new Color(1f, 0.25f, 0.75f),
        new Color(0.15f, 0.7f, 1f)
    };

    [MenuItem("Tools/Case/Decora perimetro delle case")]
    private static void ApriTool()
    {
        GetWindow<HousePerimeterDecorationTool>("Decora case");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Decorazione perimetro case", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Trova le case con HousePaletteRecolorer e aggiunge arbusti e fiorellini colorati.",
            MessageType.Info);

        arbustiPerCasa = Mathf.Clamp(EditorGUILayout.IntField("Arbusti per casa", arbustiPerCasa), 0, 50);
        fioriPerCasa = Mathf.Clamp(EditorGUILayout.IntField("Fiorellini per casa", fioriPerCasa), 0, 100);
        distanza = Mathf.Clamp(EditorGUILayout.FloatField("Distanza dalla casa", distanza), 0f, 5f);
        seme = EditorGUILayout.IntField("Seme", seme);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Case da decorare", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Trascina qui le case dalla Hierarchy oppure seleziona una casa e premi 'Aggiungi selezione'.", MessageType.None);
        for (int i = 0; i < caseManuali.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            caseManuali[i] = (GameObject)EditorGUILayout.ObjectField(caseManuali[i], typeof(GameObject), true);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                caseManuali.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Aggiungi campo"))
            caseManuali.Add(null);
        if (GUILayout.Button("Aggiungi selezione"))
        {
            if (Selection.activeGameObject != null && !caseManuali.Contains(Selection.activeGameObject))
                caseManuali.Add(Selection.activeGameObject);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("TROVA CASE"))
        {
            List<GameObject> trovate = TrovaCase();
            caseManuali.Clear();
            caseManuali.AddRange(trovate);
            Debug.Log("HousePerimeterDecorationTool: trovate " + trovate.Count + " case.");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab da usare", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Trascina prefab di arbusti e fiori dal Project. Se una lista resta vuota, viene usata la decorazione procedurale.", MessageType.None);
        DisegnaListaPrefab("Arbusti", prefabArbusti);
        DisegnaListaPrefab("Fiori", prefabFiori);

        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("GENERA ARBUSTI E FIORI", GUILayout.Height(35f)))
            Genera();
        if (GUILayout.Button("RIMUOVI DECORAZIONI GENERATE"))
            Rimuovi();
        GUI.enabled = true;
    }

    private static List<GameObject> TrovaCase()
    {
        List<GameObject> result = new List<GameObject>();
        MonoBehaviour[] componenti = Object.FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour componente in componenti)
        {
            if (componente != null &&
                componente.GetType().Name == "HousePaletteRecolorer" &&
                componente.gameObject.scene.IsValid())
                result.Add(componente.gameObject);
        }
        return result;
    }

    private void Genera()
    {
        List<GameObject> caseDaDecorare = caseManuali.Count > 0 ?
            new List<GameObject>(caseManuali) : TrovaCase();
        if (caseDaDecorare.Count == 0)
        {
            Debug.LogWarning("Inserisci almeno una casa nel campo manuale oppure usa TROVA CASE.");
            return;
        }

        Random.InitState(seme);
        GameObject radice = new GameObject("Decorazioni perimetro case");
        Undo.RegisterCreatedObjectUndo(radice, "Decora perimetro case");

        foreach (GameObject casa in caseDaDecorare)
        {
            if (casa == null || !casa.scene.IsValid())
                continue;

            Bounds bounds;
            if (!TrovaBounds(casa, out bounds))
                continue;

            GameObject gruppo = new GameObject(casa.name + " - vegetazione");
            gruppo.transform.SetParent(radice.transform);

            for (int i = 0; i < arbustiPerCasa; i++)
            {
                Vector3 posizione = PuntoPerimetro(bounds, i, arbustiPerCasa, distanza);
                if (!CreaPrefab(gruppo.transform, posizione, prefabArbusti, "Arbusto"))
                    CreaArbusto(gruppo.transform, posizione);
            }

            for (int i = 0; i < fioriPerCasa; i++)
            {
                Vector3 posizione = PuntoPerimetro(bounds, i, fioriPerCasa, distanza);
                if (!CreaPrefab(gruppo.transform, posizione, prefabFiori, "Fiore"))
                    CreaFiore(gruppo.transform, posizione);
            }
        }

        EditorSceneManager.MarkSceneDirty(radice.scene);
        Selection.activeGameObject = radice;
        Debug.Log("Decorazioni generate. Premi Ctrl+Z per annullare oppure salva la scena.");
    }

    private static void DisegnaListaPrefab(string titolo, List<GameObject> lista)
    {
        EditorGUILayout.LabelField(titolo, EditorStyles.miniBoldLabel);
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

        if (GUILayout.Button("+ Aggiungi prefab " + titolo.ToLowerInvariant(), EditorStyles.miniButton))
            lista.Add(null);
    }

    private static bool CreaPrefab(Transform genitore, Vector3 posizione, List<GameObject> lista, string nome)
    {
        List<GameObject> validi = new List<GameObject>();
        foreach (GameObject prefab in lista)
            if (prefab != null && EditorUtility.IsPersistent(prefab))
                validi.Add(prefab);

        if (validi.Count == 0)
            return false;

        GameObject sorgente = validi[Random.Range(0, validi.Count)];
        GameObject copia = (GameObject)PrefabUtility.InstantiatePrefab(sorgente);
        copia.name = nome + " - decorazione";
        copia.transform.SetParent(genitore, true);
        copia.transform.position = posizione;
        copia.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        copia.transform.localScale *= Random.Range(0.8f, 1.2f);
        Undo.RegisterCreatedObjectUndo(copia, "Aggiungi " + nome);
        return true;
    }

    private static bool TrovaBounds(GameObject casa, out Bounds bounds)
    {
        Renderer[] renderer = casa.GetComponentsInChildren<Renderer>();
        if (renderer.Length == 0)
        {
            bounds = new Bounds(casa.transform.position, Vector3.one);
            return false;
        }

        bounds = renderer[0].bounds;
        for (int i = 1; i < renderer.Length; i++)
            bounds.Encapsulate(renderer[i].bounds);
        return true;
    }

    private static Vector3 PuntoPerimetro(Bounds b, int indice, int totale, float fuori)
    {
        float perimetro = 2f * (b.size.x + b.size.z);
        float d = (indice + 0.5f) / Mathf.Max(1, totale) * perimetro;
        float x;
        float z;

        if (d < b.size.x)
        {
            x = b.min.x + d;
            z = b.min.z - fuori;
        }
        else if ((d -= b.size.x) < b.size.z)
        {
            x = b.max.x + fuori;
            z = b.min.z + d;
        }
        else if ((d -= b.size.z) < b.size.x)
        {
            x = b.max.x - d;
            z = b.max.z + fuori;
        }
        else
        {
            d -= b.size.x;
            x = b.min.x - fuori;
            z = b.max.z - d;
        }

        return new Vector3(x, b.min.y, z);
    }

    private static void CreaArbusto(Transform genitore, Vector3 posizione)
    {
        GameObject arbusto = new GameObject("Arbusto colorato");
        arbusto.transform.SetParent(genitore);
        arbusto.transform.position = posizione;
        arbusto.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        arbusto.transform.localScale = Vector3.one * Random.Range(0.75f, 1.25f);

        for (int i = 0; i < 3; i++)
        {
            GameObject foglia = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foglia.name = "Foglia";
            foglia.transform.SetParent(arbusto.transform);
            foglia.transform.localPosition = new Vector3(Random.Range(-0.25f, 0.25f), 0.3f + i * 0.12f, Random.Range(-0.2f, 0.2f));
            foglia.transform.localScale = Vector3.one * Random.Range(0.35f, 0.55f);
            ImpostaColore(foglia, new Color(Random.Range(0.1f, 0.3f), Random.Range(0.55f, 0.85f), Random.Range(0.1f, 0.35f)));
            RimuoviCollider(foglia);
        }
    }

    private static void CreaFiore(Transform genitore, Vector3 posizione)
    {
        GameObject fiore = new GameObject("Fiore colorato");
        fiore.transform.SetParent(genitore);
        fiore.transform.position = posizione;
        fiore.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        fiore.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);

        GameObject stelo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stelo.name = "Stelo";
        stelo.transform.SetParent(fiore.transform);
        stelo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        stelo.transform.localScale = new Vector3(0.025f, 0.25f, 0.025f);
        ImpostaColore(stelo, new Color(0.1f, 0.65f, 0.15f));
        RimuoviCollider(stelo);

        GameObject petalo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        petalo.name = "Petalo";
        petalo.transform.SetParent(fiore.transform);
        petalo.transform.localPosition = new Vector3(0f, 0.53f, 0f);
        petalo.transform.localScale = Vector3.one * 0.17f;
        ImpostaColore(petalo, ColoriFiori[Random.Range(0, ColoriFiori.Length)]);
        RimuoviCollider(petalo);
    }

    private static void ImpostaColore(GameObject oggetto, Color colore)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return;

        Material materiale = new Material(shader);
        materiale.color = colore;
        if (materiale.HasProperty("_BaseColor"))
            materiale.SetColor("_BaseColor", colore);
        oggetto.GetComponent<Renderer>().sharedMaterial = materiale;
    }

    private static void RimuoviCollider(GameObject oggetto)
    {
        Collider collider = oggetto.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static void Rimuovi()
    {
        List<GameObject> daRimuovere = new List<GameObject>();
        foreach (GameObject oggetto in Object.FindObjectsOfType<GameObject>())
        {
            if (oggetto.name.StartsWith("Decorazioni perimetro case"))
                daRimuovere.Add(oggetto);
        }

        foreach (GameObject oggetto in daRimuovere)
            Undo.DestroyObjectImmediate(oggetto);

        if (daRimuovere.Count > 0)
            EditorSceneManager.MarkSceneDirty(daRimuovere[0].scene);
    }
}
