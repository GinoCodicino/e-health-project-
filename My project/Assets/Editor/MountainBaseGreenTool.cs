using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class MountainBaseGreenTool : EditorWindow
{
    private readonly List<GameObject> montagne = new List<GameObject>();
    private float altezzaFascia = 1.5f;
    private Color coloreVerde = new Color(0.20f, 0.48f, 0.16f, 1f);

    [MenuItem("Tools/Montagne/Colora fascia bassa di verde")]
    private static void ApriTool()
    {
        GetWindow<MountainBaseGreenTool>("Fascia verde montagne");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Fascia verde alla base delle montagne", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Seleziona le montagne da modificare. Il tool separa i triangoli nella fascia bassa e assegna loro un materiale verde.",
            MessageType.Info);

        altezzaFascia = Mathf.Clamp(EditorGUILayout.FloatField("Altezza fascia", altezzaFascia), 0.05f, 20f);
        coloreVerde = EditorGUILayout.ColorField("Colore verde", coloreVerde);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Montagne da modificare", EditorStyles.boldLabel);
        for (int i = 0; i < montagne.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            montagne[i] = (GameObject)EditorGUILayout.ObjectField(montagne[i], typeof(GameObject), true);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                montagne.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Aggiungi campo"))
            montagne.Add(null);
        if (GUILayout.Button("Aggiungi selezione"))
        {
            if (Selection.activeGameObject != null && !montagne.Contains(Selection.activeGameObject))
                montagne.Add(Selection.activeGameObject);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Trova RidgePeak / MountainRidge"))
            TrovaMontagne();

        EditorGUILayout.Space();
        if (GUILayout.Button("APPLICA FASCIA VERDE", GUILayout.Height(35f)))
            Applica();
    }

    private void TrovaMontagne()
    {
        montagne.Clear();
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                string nome = child.name.ToLowerInvariant();
                if ((nome.StartsWith("ridgepeak") || nome.StartsWith("mountainridge")) && !montagne.Contains(child.gameObject))
                    montagne.Add(child.gameObject);
            }
        }
        Repaint();
    }

    private void Applica()
    {
        if (montagne.Count == 0)
        {
            Debug.LogWarning("Aggiungi almeno una montagna o usa Trova RidgePeak / MountainRidge.");
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("Non trovo uno shader compatibile per il materiale verde.");
            return;
        }

        Material verde = new Material(shader);
        verde.name = "Mountain_Base_Green";
        verde.color = coloreVerde;
        if (verde.HasProperty("_BaseColor")) verde.SetColor("_BaseColor", coloreVerde);

        int modificati = 0;
        foreach (GameObject montagna in montagne)
        {
            if (montagna == null || !montagna.scene.IsValid()) continue;
            if (!ApplicaAllaMontagna(montagna, verde)) continue;
            modificati++;
        }

        if (modificati > 0)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        Debug.Log("Fascia verde applicata a " + modificati + " montagne. Salva la scena.");
    }

    private bool ApplicaAllaMontagna(GameObject montagna, Material verde)
    {
        Renderer[] renderer = montagna.GetComponentsInChildren<Renderer>();
        if (renderer.Length == 0) return false;

        Bounds bounds = renderer[0].bounds;
        for (int i = 1; i < renderer.Length; i++) bounds.Encapsulate(renderer[i].bounds);
        float quotaMassima = bounds.min.y + altezzaFascia;
        bool modificata = false;

        foreach (MeshFilter filtro in montagna.GetComponentsInChildren<MeshFilter>())
        {
            Mesh sorgente = filtro.sharedMesh;
            if (sorgente == null) continue;

            Vector3[] vertici;
            try { vertici = sorgente.vertices; }
            catch
            {
                Debug.LogWarning("La mesh '" + sorgente.name + "' non e' leggibile. Abilita Read/Write nelle import settings.");
                continue;
            }

            Mesh nuovaMesh = Object.Instantiate(sorgente);
            nuovaMesh.name = sorgente.name + "_FasciaVerde";
            List<int> triangoliBassi = new List<int>();
            bool haTriangoliBassi = false;

            for (int sub = 0; sub < sorgente.subMeshCount; sub++)
            {
                int[] triangoli = sorgente.GetTriangles(sub);
                List<int> triangoliNormali = new List<int>();
                for (int i = 0; i + 2 < triangoli.Length; i += 3)
                {
                    Vector3 a = filtro.transform.TransformPoint(vertici[triangoli[i]]);
                    Vector3 b = filtro.transform.TransformPoint(vertici[triangoli[i + 1]]);
                    Vector3 c = filtro.transform.TransformPoint(vertici[triangoli[i + 2]]);
                    float altezzaMedia = (a.y + b.y + c.y) / 3f;
                    if (altezzaMedia <= quotaMassima)
                    {
                        triangoliBassi.Add(triangoli[i]);
                        triangoliBassi.Add(triangoli[i + 1]);
                        triangoliBassi.Add(triangoli[i + 2]);
                        haTriangoliBassi = true;
                    }
                    else
                    {
                        triangoliNormali.Add(triangoli[i]);
                        triangoliNormali.Add(triangoli[i + 1]);
                        triangoliNormali.Add(triangoli[i + 2]);
                    }
                }
                nuovaMesh.SetTriangles(triangoliNormali, sub);
            }

            if (!haTriangoliBassi)
            {
                Object.DestroyImmediate(nuovaMesh);
                continue;
            }

            nuovaMesh.subMeshCount = sorgente.subMeshCount + 1;
            nuovaMesh.SetTriangles(triangoliBassi, sorgente.subMeshCount);
            nuovaMesh.RecalculateBounds();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Mountain_" + montagna.name + "_FasciaVerde.asset");
            AssetDatabase.CreateAsset(nuovaMesh, path);
            filtro.sharedMesh = nuovaMesh;
            EditorUtility.SetDirty(filtro);

            MeshRenderer meshRenderer = filtro.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Material[] materiali = meshRenderer.sharedMaterials;
                Material[] nuoviMateriali = new Material[sorgente.subMeshCount + 1];
                for (int i = 0; i < sorgente.subMeshCount; i++)
                    nuoviMateriali[i] = i < materiali.Length ? materiali[i] : null;
                nuoviMateriali[sorgente.subMeshCount] = verde;
                meshRenderer.sharedMaterials = nuoviMateriali;
                EditorUtility.SetDirty(meshRenderer);
            }
            modificata = true;
        }
        return modificata;
    }
}
