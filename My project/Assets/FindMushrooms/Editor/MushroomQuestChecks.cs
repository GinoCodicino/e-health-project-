using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MushroomQuestChecks
{
    [MenuItem("Tools/Find Mushrooms/Verifica dati e progressi quiz")]
    private static void Run()
    {
        GameObject temporary = null;
        try
        {
            TextAsset asset = Resources.Load<TextAsset>("MushroomLesson");
            Require(asset != null, "Manca MushroomLesson.json nella cartella Resources.");
            temporary = new GameObject("QuestDataCheck") { hideFlags = HideFlags.HideAndDontSave };
            MushroomLearningQuest quest = temporary.AddComponent<MushroomLearningQuest>();
            quest.lessonAsset = asset;
            Require(quest.IsAvailable, "Dati delle domande non validi; controlla la Console.");
            MushroomLearningQuest.Lesson data = JsonUtility.FromJson<MushroomLearningQuest.Lesson>(asset.text);
            List<string> topics = new List<string>();
            Dictionary<string, string> keys = new Dictionary<string, string>();
            foreach (MushroomLearningQuest.Topic topic in data.topics) topics.Add(topic.id);
            foreach (MushroomLearningQuest.Question question in data.questions) keys.Add(question.id, question.correct);
            MushroomQuizState state = new MushroomQuizState(topics, keys);
            Require(!state.CanStart(true), "La verifica non deve aprirsi senza dialoghi.");
            Require(!state.Submit(data.questions[0].id, data.questions[0].correct, true), "Risposta accettata prima dei dialoghi.");
            state.MarkTopicRead("unknown");
            Require(state.TopicCount == 0, "Argomento inesistente accettato.");
            foreach (string topic in topics) { state.MarkTopicRead(topic); state.MarkTopicRead(topic); }
            Require(state.TopicCount == 3 && !state.CanStart(false) && state.CanStart(true), "Blocco dialoghi/funghi non corretto.");
            foreach (MushroomLearningQuest.Question question in data.questions)
            {
                int before = state.PassedCount;
                Require(!state.Submit(question.id, "wrong", true) && state.PassedCount == before, "Errore conteggiato come risposta corretta.");
                Require(state.Submit(question.id, question.correct, true), "Risposta corretta rifiutata.");
                Require(state.Submit(question.id, question.correct, true) && state.PassedCount == before + 1, "Risposta contata due volte.");
            }
            Require(state.IsComplete && state.TopicCount == 3 && state.PassedCount == 3, "Completamento non corretto.");
            Require(!state.Submit(data.questions[0].id, "wrong", true) && state.IsComplete, "Un errore ha cancellato i progressi.");
            EditorUtility.DisplayDialog("Find Mushrooms", "Controlli superati: dati validi, dialoghi e funghi obbligatori, errori senza perdita di progressi, completamento a 3/3.", "OK");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Find Mushrooms", "Verifica fallita: " + error.Message, "OK");
        }
        finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
