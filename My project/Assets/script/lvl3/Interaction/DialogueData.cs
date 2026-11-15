using System;
using UnityEngine;

/// <summary>One line of dialogue. Empty speaker = the NPC who started the dialogue.</summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
    [Tooltip("Where the fact of this line comes from (shown under the text), e.g. \"IPCC AR6 SYR, SPM A.2.2, p.11\".")]
    public string source;
    [Tooltip("If not empty, the player picks one of these answers after this line (keys 1, 2, 3).")]
    public DialogueChoice[] choices;
}

/// <summary>An answer the player can choose, followed by the NPC's reaction.</summary>
[Serializable]
public class DialogueChoice
{
    [Tooltip("Short text in the list of answers.")]
    public string label;
    [TextArea(2, 5)] public string playerLine;
    public string source;
    [TextArea(2, 5)] public string reaction;
    [Tooltip("How much this answer convinces the NPC (adds up over the dialogue and picks the ending).")]
    public int convince;
}

/// <summary>Last line of the dialogue, used when the NPC was convinced at least minConvince.</summary>
[Serializable]
public class DialogueEnding
{
    public int minConvince;
    [TextArea(2, 5)] public string text;
}

/// <summary>
/// A dialogue, edited as an asset: right-click in the Project → Create → Dialogue.
/// Linear, or with choices of the player and endings that depend on them.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue", fileName = "NewDialogue")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
    [Tooltip("Optional: the one with the highest minConvince reached ends the dialogue.")]
    public DialogueEnding[] endings;
}
