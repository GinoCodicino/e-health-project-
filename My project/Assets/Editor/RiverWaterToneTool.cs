using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class RiverWaterToneTool : EditorWindow
{
    private MeshRenderer acqua;
    private Color coloreAcqua = new Color(0.04f, 0.24f, 0.38f, 0.82f);
    private bool acquaMenoTrasparente = true;

    [MenuItem("Tools/Fiume/Scurisci acqua")]
    private static void ApriTool()
    {
        GetWindow<RiverWaterToneTool>("Colore acqua");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Colore acqua del fiume", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Crea una copia del materiale dell'acqua e la rende piu' scura e meno limpida senza modificare l'asset originale.",
            MessageType.Info);

        acqua = (MeshRenderer)EditorGUILayout.ObjectField(
            "Water Plane", acqua, typeof(MeshRenderer), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Trova acqua"))
            acqua = TrovaAcqua();
        if (GUILayout.Button("Usa selezione"))
            acqua = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<MeshRenderer>()
                : null;
        EditorGUILayout.EndHorizontal();

        coloreAcqua = EditorGUILayout.ColorField("Colore acqua", coloreAcqua);
        acquaMenoTrasparente = EditorGUILayout.Toggle("Meno trasparente", acquaMenoTrasparente);

        EditorGUILayout.Space();
        if (GUILayout.Button("APPLICA COLORE ACQUA", GUILayout.Height(32f)))
            Applica();
    }

    private static MeshRenderer TrovaAcqua()
    {
        MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            string nome = renderer.name.ToLowerInvariant();
            if (nome.Contains("water") || nome.Contains("acqua") || nome.Contains("river"))
                return renderer;
        }
        return null;
    }

    private void Applica()
    {
        if (acqua == null)
        {
            Debug.LogWarning("Assegna il MeshRenderer del Water Plane oppure premi Trova acqua.");
            return;
        }

        Material[] materiali = acqua.sharedMaterials;
        Material[] copie = new Material[materiali.Length];
        for (int i = 0; i < materiali.Length; i++)
        {
            if (materiali[i] == null)
                continue;

            Material copia = new Material(materiali[i]);
            copia.name = materiali[i].name + "_AcquaScura";
            Color colore = coloreAcqua;
            if (!acquaMenoTrasparente)
                colore.a = materiali[i].color.a;

            copia.color = colore;
            if (copia.HasProperty("_BaseColor"))
                copia.SetColor("_BaseColor", colore);
            if (copia.HasProperty("_Color"))
                copia.SetColor("_Color", colore);

            copie[i] = copia;
        }

        Undo.RecordObject(acqua, "Scurisci acqua del fiume");
        acqua.sharedMaterials = copie;
        EditorSceneManager.MarkSceneDirty(acqua.gameObject.scene);
        Debug.Log("Materiale dell'acqua duplicato e scurito. Salva la scena per conservare la modifica.");
    }
}
