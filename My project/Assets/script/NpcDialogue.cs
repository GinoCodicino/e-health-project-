using TMPro;
using UnityEngine;

// Sostituire il CONTENUTO del vecchio NpcDialogue.cs mantenendone il .meta.
// Nessuna richiesta di rete: i metodi sotto mantengono compilabili i vecchi script.
public class NpcDialogue : ForestQuest
{
    [HideInInspector] public GameObject dialoguePanel;
    [HideInInspector] public TMP_Text npcText;
    [HideInInspector] public TMP_InputField playerInputField;
    [HideInInspector] public FindMushroomsGame mushroomGame;
    [HideInInspector] public MushroomLearningQuest learningQuest;
    [HideInInspector] public bool buildMushroomInterface;
    protected override void Awake()
    {
        if (npcText != null && npcText.font != null) MushroomUI.Font = npcText.font;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (playerInputField != null) playerInputField.gameObject.SetActive(false);
        base.Awake();
    }
    public void SkipIntroduction() { ShowAll(); }
    public void OpenAfterGame(playercontroller who,bool complete) { CloseDialogue(); OpenDialogue(who); }
    public void OpenFromLearning(playercontroller who) { CloseDialogue(); OpenDialogue(who); }
    public void AskSuggestedQuestion(string ignored) { }
    public void OnSubmitQuestion() { }
    public void BeginDiscussion() { }
    public void BeginAssessment() { }
    public void OpenComposer() { }
    public void CloseComposer() { }
    public void SubmitComposer() { }
}
