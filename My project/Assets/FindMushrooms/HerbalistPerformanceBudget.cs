using UnityEngine;

[DisallowMultipleComponent]
public class HerbalistPerformanceBudget : MonoBehaviour
{
    private NpcDialogue dialogue;
    private bool limited;
    private int previousRate;
    private int previousVSync;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject npc = GameObject.Find("NPC_funghi");
        if (npc == null || npc.GetComponent<NpcDialogue>() == null) return;
        if (npc.GetComponent<HerbalistPerformanceBudget>() == null)
            npc.AddComponent<HerbalistPerformanceBudget>();
    }

    private void Awake()
    {
        dialogue = GetComponent<NpcDialogue>();
    }

    private void Update()
    {
        if (dialogue == null)
        {
            Restore();
            return;
        }

        bool panelOpen = dialogue.IsOpen
            || (dialogue.learningQuest != null
                && dialogue.learningQuest.IsOpen)
            || (dialogue.mushroomGame != null
                && dialogue.mushroomGame.IsOpen);

        if (!panelOpen)
        {
            Restore();
            return;
        }

        var input = dialogue.playerInputField;
        bool waiting = dialogue.IsOpen && input != null
            && input.gameObject.activeInHierarchy
            && !input.interactable;

        Apply(waiting ? 15 : 30);
    }

    private void Apply(int rate)
    {
        if (!limited)
        {
            previousRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            limited = true;
        }

        if (QualitySettings.vSyncCount == 0
            && Application.targetFrameRate == rate) return;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = rate;
        Debug.Log("Herbalist frame cap: " + rate + " FPS", this);
    }

    private void Restore()
    {
        if (!limited) return;
        Application.targetFrameRate = previousRate;
        QualitySettings.vSyncCount = previousVSync;
        limited = false;
    }

    private void OnDisable()
    {
        Restore();
    }
}
