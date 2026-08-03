using Meta.XR.BuildingBlocks.AIBlocks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public sealed class VoiceMemoNote : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpeechToTextAgent speechToText;
    [SerializeField] private TMP_Text memoText;
    [SerializeField] private TMP_Text statusText;

    [Header("Memo")]
    [SerializeField] private bool appendText = true;
    [SerializeField] private string separator = "\n";

    //[Header("Input")]
    //[SerializeField] private OVRHand leftHand;

    //private bool wasOK;

#if UNITY_ANDROID && !UNITY_EDITOR
    private PermissionCallbacks permissionCallbacks;
#endif

    private void OnEnable()
    {
        if (speechToText != null)
        {
            speechToText.onTranscript.AddListener(ApplyTranscript);
        }
    }

    private void OnDisable()
    {
        if (speechToText != null)
        {
            speechToText.onTranscript.RemoveListener(ApplyTranscript);
        }
    }

    private void Update()
    {
        //// Quest 컨트롤러 A 버튼
        //if (OVRInput.GetDown(questStartButton))
        //{
        //    StartDictation();
        //}

#if UNITY_EDITOR
        // Unity 에디터 테스트용 Space 키
        if (Keyboard.current?.spaceKey.wasPressedThisFrame == true)
        {
            StartDictation();
        }
#endif
    }

    // UI Button의 OnClick에도 연결할 수 있습니다.
    public void StartDictation()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            SetStatus("마이크 권한을 허용해 주세요.");

            permissionCallbacks = new PermissionCallbacks();
            permissionCallbacks.PermissionGranted += OnMicrophoneGranted;
            permissionCallbacks.PermissionDenied += OnMicrophoneDenied;
            permissionCallbacks.PermissionDeniedAndDontAskAgain +=
                OnMicrophoneDenied;

            Permission.RequestUserPermission(
                Permission.Microphone,
                permissionCallbacks);

            return;
        }
#endif

        BeginListening();
    }

    public void StopDictation()
    {
        if (speechToText == null)
            return;

        speechToText.StopNow();
        SetStatus("음성을 텍스트로 변환 중...");
    }

    public void ClearMemo()
    {
        if (memoText != null)
        {
            memoText.text = string.Empty;
        }

        SetStatus(string.Empty);
    }

    private void BeginListening()
    {
        if (speechToText == null)
        {
            Debug.LogError("SpeechToTextAgent가 연결되지 않았습니다.", this);
            return;
        }

        SetStatus("말씀하세요...");
        speechToText.StartListening();
    }

    private void ApplyTranscript(string transcript)
    {
        if (memoText == null)
        {
            Debug.LogError("메모지 TMP_Text가 연결되지 않았습니다.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus("인식된 음성이 없습니다.");
            return;
        }

        string newText = transcript.Trim();

        if (appendText && !string.IsNullOrWhiteSpace(memoText.text))
        {
            memoText.text =
                memoText.text.TrimEnd() + separator + newText;
        }
        else
        {
            memoText.text = newText;
        }

        SetStatus("입력 완료");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnMicrophoneGranted(string permission)
    {
        permissionCallbacks = null;
        BeginListening();
    }

    private void OnMicrophoneDenied(string permission)
    {
        permissionCallbacks = null;
        SetStatus("마이크 권한이 거부되었습니다.");
    }
#endif
}