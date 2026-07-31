using UnityEngine;
using Meta.XR.BuildingBlocks.AIBlocks;
using UnityEngine.InputSystem;
using System.Runtime.CompilerServices; // VR이 아닌 키보드를 입력으로
using System.Threading.Tasks;

public class AIManager : MonoBehaviour
{
    public AIProviderBase providerAsset;

    public SpeechToTextAgent speechToText;
    public TextToSpeechAgent textToSpeech;
    public LlmAgent llmAgent;

    public OVRInput.Button activateButton = OVRInput.Button.One; // Reference to certain button

    private IChatTask chatTask;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chatTask = providerAsset as IChatTask;

        speechToText.onTranscript.AddListener(OnTranscript);
    }

    // Update is called once per frame
    void Update()
    {
        bool isPressed = OVRInput.GetDown(activateButton) || Keyboard.current.spaceKey.wasPressedThisFrame;

        if (isPressed)
        {
            speechToText.StartListening(); // speechToText를 호출하여 Listen을 시작
        }
    }

    private void OnTranscript(string transcript)
    {
        AskAI(transcript);
    }

    private async Task AskAI(string userText) //
    {
        ChatRequest request = new ChatRequest(userText);
        ChatResponse response = await chatTask.ChatAsync(request); //

        Debug.Log("Response Raw " + response.Raw);
        Debug.Log("Response Text " + response.text);

        textToSpeech.SpeakText(response.text);
    }
}
