using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Level02.BridgeMission;

// Mettere in una cartella Editor (accanto a BridgeQuestSetup.cs).
public static class BeamCuttingSetup
{
    const string Folder = "Assets/Level02/BridgeQuest/Materials";

    [MenuItem("Tools/Level 02/Bridge quest/Add beam cutting minigame")]
    static void AddToExisting()
    {
        if (Application.isPlaying) { Debug.LogWarning("Exit Play mode first."); return; }
        BridgeQuestController quest = Object.FindFirstObjectByType<BridgeQuestController>();
        if (quest == null) { Debug.LogError("No bridge quest found. Run 'Configure selected bridge' first."); return; }
        if (Object.FindFirstObjectByType<BeamCuttingGame>() != null)
        { Debug.LogWarning("Beam cutting minigame already exists. Edit its Inspector instead."); return; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add beam cutting minigame");
        GameObject go = Create(quest, quest.transform);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(quest.gameObject.scene);
        Selection.activeGameObject = go;
        Debug.Log("Beam cutting minigame created next to the woodcutter. Move it onto flat ground (it is already linked to the bridge quest). Save the scene.", go);
    }

    // Chiamato anche da BridgeQuestSetup.Configure.
    internal static GameObject Create(BridgeQuestController quest, Transform parent)
    {
        Vector3 basePos = quest.woodcutter != null ? quest.woodcutter.position : quest.transform.position;
        Vector3 toPlayer = quest.player != null ? quest.player.transform.position - basePos : Vector3.back;
        toPlayer.y = 0;
        if (toPlayer.sqrMagnitude < .01f) toPlayer = Vector3.back;
        toPlayer.Normalize();

        // La trave sta di fianco al boscaiolo, vista di lato da chi arriva.
        Vector3 pos = basePos + Vector3.Cross(Vector3.up, toPlayer) * 3f;
        if (quest.bridge != null && quest.bridge.intactBridge != null && quest.bridge.brokenBridge != null && quest.player != null)
            pos = BridgeQuestSetup.FindGround(pos, quest.bridge.intactBridge, quest.bridge.brokenBridge, quest.player);

        GameObject go = new GameObject("BeamCutting_MOVE_NEXT_TO_WOODCUTTER");
        Undo.RegisterCreatedObjectUndo(go, "Create beam cutting game");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toPlayer, Vector3.up));

        BeamCuttingGame game = Undo.AddComponent<BeamCuttingGame>(go);
        game.player = quest.player;
        game.woodMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/BrokenWood.mat");
        game.selfInteract = false; // E e gestito da BridgeQuestController
        game.freezePlayer = false;
        EditorUtility.SetDirty(game);
        Undo.RecordObject(quest, "Link beam game");
        quest.beamGame = game;
        EditorUtility.SetDirty(quest);
        return go;
    }
}
