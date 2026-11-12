using UnityEngine;
using UnityEditor;

public class ReplaceObjectsTool : EditorWindow
{
    private GameObject newPrefab;

    [MenuItem("Tools/Sostituisci Oggetti")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceObjectsTool>("Sostituisci Oggetti");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sostituisci oggetti selezionati con un nuovo Prefab", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        newPrefab = (GameObject)EditorGUILayout.ObjectField("Nuovo Prefab/Modello", newPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Sostituisci Selezionati"))
        {
            if (newPrefab == null)
            {
                EditorUtility.DisplayDialog("Errore", "Trascina prima il nuovo Prefab nel campo apposito!", "OK");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Errore", "Seleziona almeno un oggetto nella scena da sostituire!", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(Selection.gameObjects, "Sostituisci Oggetti");

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                GameObject oldObj = selectedObjects[i];

                // Istanzia il nuovo oggetto mantenendo il collegamento come Prefab se applicabile
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab);
                if (newObj == null)
                {
                    newObj = Instantiate(newPrefab);
                }

                // Registra l'azione per poter fare Undo (Ctrl+Z) se necessario
                Undo.RegisterCreatedObjectUndo(newObj, "Crea nuovo oggetto");

                // Assegna genitore e coordinate esatte
                newObj.transform.SetParent(oldObj.transform.parent);
                newObj.transform.position = oldObj.transform.position;
                newObj.transform.rotation = oldObj.transform.rotation;
                newObj.transform.localScale = oldObj.transform.localScale;
                newObj.transform.SetSiblingIndex(oldObj.transform.GetSiblingIndex());

                // Distrugge il vecchio oggetto
                Undo.DestroyObjectImmediate(oldObj);
            }
        }
    }
}
