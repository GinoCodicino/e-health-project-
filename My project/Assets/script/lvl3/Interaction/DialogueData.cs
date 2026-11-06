using System;
using UnityEngine;

/// <summary>One line of dialogue. Empty speaker = the NPC who started the dialogue.</summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}

/// <summary>
/// A linear dialogue, edited as an asset: right-click in the Project → Create → Dialogue.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue", fileName = "NewDialogue")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
}
