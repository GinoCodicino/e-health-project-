using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.EventSystems;

public class NpcDialogue : MonoBehaviour
{
    [Header("Riferimenti UI")]
    public GameObject dialoguePanel;
    public TMP_Text npcText;
    public TMP_InputField playerInputField;

    [Header("Configurazione Dialogo")]
    [TextArea(3, 10)]
    public string introSpeech = "Welcome, traveler. Look around you... our precious mushrooms have stopped growing. The soil has fallen ill ever since the ancient roots lost their nourishment. Tell me, what do you wish to know?";
    public float typingSpeed = 0.03f;

    [Header("Server LLM")]
    public string apiUrl = "http://127.0.0.1:8000/ask";

    private bool hasSpoken = false;
    private bool isWaitingResponse = false;

    void Start()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);

        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(false);
            // Invia la domanda premendo INVIO sulla tastiera
            playerInputField.onSubmit.AddListener(delegate { OnSubmitQuestion(); });
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.name.ToLower().Contains("player"))
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(true);

            // Sblocca il cursore del mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!hasSpoken)
            {
                StartCoroutine(TypeIntroRoutine());
                hasSpoken = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.name.ToLower().Contains("player"))
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            // Riblocca il cursore per il controllo del giocatore
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    IEnumerator TypeIntroRoutine()
    {
        npcText.text = "";
        foreach (char letter in introSpeech.ToCharArray())
        {
            npcText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        yield return new WaitForSeconds(1.5f);
        npcText.text = ""; // Libera lo spazio per visualizzare la risposta

        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(true);
            playerInputField.text = "";

            yield return new WaitForEndOfFrame();

            // Mette a fuoco la casella e fa comparire subito il cursore per scrivere
            EventSystem.current.SetSelectedGameObject(playerInputField.gameObject);
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }
    }

    public void OnSubmitQuestion()
    {
        if (isWaitingResponse) return;
        string question = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(question)) return;

        StartCoroutine(SendQuestionToPython(question));
    }

    IEnumerator SendQuestionToPython(string playerQuestion)
    {
        isWaitingResponse = true;

        playerInputField.gameObject.SetActive(false);
        npcText.text = "The herbalist is thinking...";

        QuestionData data = new QuestionData { question = playerQuestion };
        string jsonBody = JsonUtility.ToJson(data);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResult = request.downloadHandler.text;
                AnswerData answerData = JsonUtility.FromJson<AnswerData>(jsonResult);
                npcText.text = answerData.answer;
            }
            else
            {
                npcText.text = "He seems confused... (Cannot reach LLM server)";
                Debug.LogError("Errore LLM: " + request.error);
            }
        }

        // Riapre il campo dopo 4 secondi per permettere un'altra domanda
        yield return new WaitForSeconds(4.0f);
        playerInputField.text = "";
        playerInputField.gameObject.SetActive(true);

        yield return new WaitForEndOfFrame();
        EventSystem.current.SetSelectedGameObject(playerInputField.gameObject);
        playerInputField.ActivateInputField();
        playerInputField.Select();

        isWaitingResponse = false;
    }

    [System.Serializable]
    private class QuestionData
    {
        public string question;
    }

    [System.Serializable]
    private class AnswerData
    {
        public string answer;
    }
}