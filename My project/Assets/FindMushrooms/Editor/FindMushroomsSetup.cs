using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FindMushroomsSetup
{
    [MenuItem("Tools/Find Mushrooms/Configura scena aperta")]
    private static void Configure() { ConfigureScene(false); }

    [MenuItem("Tools/Find Mushrooms/Aggiorna dialogo trasparente")]
    private static void UpdateTransparentDialogue() { ConfigureScene(true); }

    [MenuItem("Tools/Find Mushrooms/Aggiorna dialogo e verifica")]
    private static void UpdateDialogueAndQuiz() { ConfigureScene(false); }

    private static void ConfigureScene(bool restoreOriginalIntro)
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Find Mushrooms", "Ferma Play prima di configurare la scena.", "OK");
            return;
        }
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("Find Mushrooms", "Esci dalla modifica del prefab con la freccia in alto nella Hierarchy e apri la scena scene2.", "OK");
            return;
        }
        GameObject npc = GameObject.Find("NPC_funghi");
        GameObject main = GameObject.Find("main_character");
        playercontroller player = main == null ? null : main.GetComponentInChildren<playercontroller>();
        if (player == null) player = Object.FindFirstObjectByType<playercontroller>();
        if (npc == null || player == null)
        {
            EditorUtility.DisplayDialog("Find Mushrooms", "Apri la scena con NPC_funghi e main_character. Il giocatore deve avere playercontroller attivo.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configura Find Mushrooms");
        NpcDialogue dialogue = npc.GetComponent<NpcDialogue>();
        if (dialogue == null) dialogue = Undo.AddComponent<NpcDialogue>(npc);
        FindMushroomsGame game = npc.GetComponent<FindMushroomsGame>();
        if (game == null) game = Undo.AddComponent<FindMushroomsGame>(npc);
        MushroomLearningQuest learning = npc.GetComponent<MushroomLearningQuest>();
        if (learning == null) learning = Undo.AddComponent<MushroomLearningQuest>(npc);
        Undo.RecordObject(dialogue, "Collega dialogo");
        Undo.RecordObject(game, "Collega minigioco");
        Undo.RecordObject(learning, "Collega verifica");
        dialogue.player = player;
        dialogue.mushroomGame = game;
        dialogue.learningQuest = learning;
        dialogue.buildMushroomInterface = true;
        if (dialogue.requestTimeoutSeconds < 300) dialogue.requestTimeoutSeconds = 300;
        if (string.IsNullOrWhiteSpace(dialogue.apiUrl)) dialogue.apiUrl = "http://127.0.0.1:8000/ask";
        if (restoreOriginalIntro || string.IsNullOrWhiteSpace(dialogue.introSpeech)
            || dialogue.introSpeech == NpcDialogue.PreviousPatchIntroSpeech)
            dialogue.introSpeech = NpcDialogue.OriginalIntroSpeech;
        game.player = player;
        game.dialogue = dialogue;
        learning.dialogue = dialogue;
        learning.mushroomGame = game;
        learning.lessonAsset = Resources.Load<TextAsset>("MushroomLesson");

        bool hasTrigger = false;
        foreach (Collider col in npc.GetComponentsInChildren<Collider>())
            if (col.enabled && col.isTrigger) hasTrigger = true;
        if (!hasTrigger)
        {
            SphereCollider trigger = Undo.AddComponent<SphereCollider>(npc);
            trigger.isTrigger = true;
            Vector3 scale = npc.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.01f);
            trigger.radius = 3.5f / maxScale;
            trigger.center = Vector3.up * (0.8f / Mathf.Max(Mathf.Abs(scale.y), 0.01f));
        }
        Rigidbody body = npc.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(npc);
            body.isKinematic = true;
            body.useGravity = false;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(dialogue))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(dialogue);
            PrefabUtility.RecordPrefabInstancePropertyModifications(game);
            PrefabUtility.RecordPrefabInstancePropertyModifications(learning);
        }
        EditorUtility.SetDirty(dialogue);
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(learning);
        EditorSceneManager.MarkSceneDirty(npc.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = npc;
        EditorUtility.DisplayDialog("Find Mushrooms", "Dialogo e verifica configurati. Salva con Cmd+S, premi Play e avvicinati a NPC_funghi. Write more: testo lungo. Space: mostra subito l'introduzione. Discuss his doubts: tre temi obbligatori. La verifica si sblocca con tre temi e cinque funghi. Lascia attivo il server Python per le risposte libere.", "OK");
    }
}
