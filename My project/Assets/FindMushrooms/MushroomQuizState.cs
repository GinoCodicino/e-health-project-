using System.Collections.Generic;

// Progressi della missione, indipendenti dall'LLM e dall'interfaccia.
public class MushroomQuizState
{
    private readonly HashSet<string> topics = new HashSet<string>();
    private readonly HashSet<string> passed = new HashSet<string>();
    private readonly HashSet<string> knownTopics;
    private readonly Dictionary<string, string> answerKeys;

    public int TopicCount { get { return topics.Count; } }
    public int PassedCount { get { return passed.Count; } }
    public bool IsComplete { get { return passed.Count == answerKeys.Count; } }

    public MushroomQuizState(IEnumerable<string> topicIds, IDictionary<string, string> correctAnswers)
    {
        knownTopics = new HashSet<string>(topicIds);
        answerKeys = new Dictionary<string, string>(correctAnswers);
    }

    public void MarkTopicRead(string topicId)
    {
        if (knownTopics.Contains(topicId)) topics.Add(topicId);
    }

    public bool HasRead(string topicId) { return topics.Contains(topicId); }
    public bool HasPassed(string questionId) { return passed.Contains(questionId); }

    public bool CanStart(bool mushroomsFound)
    {
        return mushroomsFound && topics.Count == knownTopics.Count;
    }

    public bool Submit(string questionId, string optionId, bool mushroomsFound)
    {
        string key;
        if (!CanStart(mushroomsFound) || !answerKeys.TryGetValue(questionId, out key) || key != optionId)
            return false;
        passed.Add(questionId);
        return true;
    }
}
