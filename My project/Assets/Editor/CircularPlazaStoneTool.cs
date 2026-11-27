using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CircularPlazaStoneTool : EditorWindow
{
    private GameObject piazzetta;
    private Color colorePietra = new Color(0.24f, 0.27f, 0.28f, 1f);
    private float scurimento = 0.65f;

    [MenuItem("Tools/Colori/Scurisci piazzetta circolare")]
    private static void ApriTool()
    {
        GetWindow<CircularPlazaStoneTool>("Piazzetta in pietra");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Piazzetta circolare in pietra", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Applica una copia scura del materiale alla piazzetta selezionata, senza modificare il materiale originale nel Project.",
            MessageType.Info);

        piazzetta = (GameObject)EditorGUILayout.ObjectField(
            "Piazzetta", piazzetta, typeof(GameObject), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usa selezione"))
            piazzetta = Selection.activeGameObject;
        if (GUILayout.Button("Trova piazzetta"))
            piazzetta = TrovaPiazzetta();
        EditorGUILayout.EndHorizontal();

        colorePietra = EditorGUILayout.ColorField("Colore pietra", colorePietra);
        scurimento = Mathf.Clamp01(EditorGUILayout.Slider("Intensita' scurimento", scurimento, 0f, 1f));

        EditorGUILayout.Space();
        if (GUILayout.Button("APPLICA MATERIALE PIETRA", GUILayout.Height(32f)))
            Applica();
    }

    private static GameObject TrovaPiazzetta()
    {
        foreach (GameObject oggetto in Object.FindObjectsOfType<GameObject>())
        {
            if (oggetto.scene.IsValid() && oggetto.name.ToLowerInvariant().Contains("piazzetta") &&
                oggetto.name.ToLowerInvariant().Contains("circolare"))
                return oggetto;
        }
        return null;
    }

    private void Applica()
    {
        if (piazzetta == null)
        {
            Debug.LogWarning("Assegna o seleziona l'oggetto piazzetta circolare.");
            return;
        }

        Color coloreFinale = Color.Lerp(Color.white, colorePietra, scurimento);
        int materialiModificati = 0;

        foreach (Renderer renderer in piazzetta.GetComponentsInChildren<Renderer>(true))
        {
            Material[] originali = renderer.sharedMaterials;
            Material[] copie = new Material[originali.Length];

            for (int i = 0; i < originali.Length; i++)
            {
                if (originali[i] == null) continue;

                Material copia = new Material(originali[i]);
                copia.name = originali[i].name + "_PietraScura";
                copia.color = coloreFinale;
                if (copia.HasProperty("_BaseColor"))
                    copia.SetColor("_BaseColor", coloreFinale);
                if (copia.HasProperty("_Color"))
                    copia.SetColor("_Color", coloreFinale);

                copie[i] = copia;
                materialiModificati++;
            }

            Undo.RecordObject(renderer, "Scurisci piazzetta circolare");
            renderer.sharedMaterials = copie;
            EditorUtility.SetDirty(renderer);
        }

        EditorSceneManager.MarkSceneDirty(piazzetta.scene);
        Debug.Log("Materiali della piazzetta modificati: " + materialiModificati + ". Salva la scena.");
    }
}
