using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

public sealed class PaperQRTracker : MonoBehaviour
{
    [Header("MRUK")]
    [SerializeField] private MRUK mruk;

    [Tooltip("처음에는 비워 두고 실제 Payload를 로그로 확인합니다.")]
    [SerializeField] private string targetPayload = "";

    [Header("Paper")]
    [SerializeField] private Transform paperRoot;

    [Tooltip("A4 종이 크기(m)")]
    [SerializeField]
    private Vector2 paperSizeMeters = new Vector2(0.210f, 0.297f);

    [Tooltip("QR 중심에서 종이 중심까지의 오프셋(m)")]
    [SerializeField]
    private Vector2 paperCenterOffsetFromQrMeters =
        new Vector2(-0.0848f, -0.1285f);

    [Header("Calibration")]
    [Tooltip("QR 로컬 축과 종이 방향의 회전 차이")]
    [SerializeField]
    private Vector3 localRotationCorrectionEuler = Vector3.zero;

    [SerializeField] private bool hideWhenUntracked = true;

    private readonly List<MRUKTrackable> existingTrackables =
        new List<MRUKTrackable>();

    private MRUKTrackable trackedQr;
    private bool listenersRegistered;

    private void Start()
    {
        if (paperRoot == null)
        {
            Debug.LogError(
                "[PaperQrTracker] PaperRoot가 연결되지 않았습니다.",
                this
            );
            enabled = false;
            return;
        }

        if (mruk == null)
        {
            mruk = MRUK.Instance;
        }

        if (mruk == null)
        {
            Debug.LogError(
                "[PaperQrTracker] MRUK를 찾을 수 없습니다.",
                this
            );
            enabled = false;
            return;
        }

        paperRoot.gameObject.SetActive(false);

        RegisterListeners();
        EnableQrTracking();

        // 스크립트가 활성화되기 전에 이미 검출된 QR도 확인합니다.
        mruk.GetTrackables(existingTrackables);

        foreach (MRUKTrackable trackable in existingTrackables)
        {
            TryBindTrackable(trackable);
        }

        Debug.Log(
            $"[PaperQrTracker] Ready. TargetPayload='{targetPayload}'",
            this
        );
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        mruk.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
        mruk.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);

        listenersRegistered = true;
    }

    private void EnableQrTracking()
    {
        OVRAnchor.TrackerConfiguration configuration =
            mruk.SceneSettings.TrackerConfiguration;

        configuration.QRCodeTrackingEnabled = true;
        mruk.SceneSettings.TrackerConfiguration = configuration;
    }

    private void OnTrackableAdded(MRUKTrackable trackable)
    {
        TryBindTrackable(trackable);
    }

    private void TryBindTrackable(MRUKTrackable trackable)
    {
        if (trackable == null ||
            trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
        {
            return;
        }

        string payload = trackable.MarkerPayloadString ?? string.Empty;

        Debug.Log(
            $"[PaperQrTracker] QR detected. " +
            $"Payload='{payload}', Tracked={trackable.IsTracked}, " +
            $"PlaneRect={trackable.PlaneRect}",
            trackable
        );

        if (!string.IsNullOrWhiteSpace(targetPayload) &&
            payload != targetPayload)
        {
            Debug.Log(
                $"[PaperQrTracker] 다른 QR 무시. " +
                $"Expected='{targetPayload}', Actual='{payload}'",
                trackable
            );
            return;
        }

        if (trackedQr != null && trackedQr != trackable)
        {
            Debug.LogWarning(
                "[PaperQrTracker] 이미 다른 QR을 추적하고 있습니다.",
                trackable
            );
            return;
        }

        trackedQr = trackable;
        BindPaperToQr(trackable);
    }

    private void BindPaperToQr(MRUKTrackable trackable)
    {
        Vector2 qrCenterLocal = trackable.PlaneRect.HasValue
            ? trackable.PlaneRect.Value.center
            : Vector2.zero;

        Quaternion correction =
            Quaternion.Euler(localRotationCorrectionEuler);

        Vector3 offsetInPaperAxes = new Vector3(
            paperCenterOffsetFromQrMeters.x,
            paperCenterOffsetFromQrMeters.y,
            0f
        );

        // 종이 축으로 측정한 Offset을 QR 로컬 축으로 회전합니다.
        Vector3 offsetInQrAxes = correction * offsetInPaperAxes;

        paperRoot.SetParent(trackable.transform, false);
        paperRoot.localRotation = correction;
        paperRoot.localPosition =
            new Vector3(qrCenterLocal.x, qrCenterLocal.y, 0f)
            + offsetInQrAxes;
        paperRoot.localScale = Vector3.one;

        paperRoot.gameObject.SetActive(
            !hideWhenUntracked || trackable.IsTracked
        );

        Debug.Log(
            $"[PaperQrTracker] PaperRoot bound. " +
            $"Payload='{trackable.MarkerPayloadString}', " +
            $"LocalPosition={paperRoot.localPosition}, " +
            $"LocalRotation={paperRoot.localEulerAngles}",
            paperRoot
        );
    }

    private void Update()
    {
        if (trackedQr == null || paperRoot == null)
        {
            return;
        }

        if (hideWhenUntracked)
        {
            bool shouldBeVisible = trackedQr.IsTracked;

            if (paperRoot.gameObject.activeSelf != shouldBeVisible)
            {
                paperRoot.gameObject.SetActive(shouldBeVisible);
            }
        }
    }

    private void OnTrackableRemoved(MRUKTrackable trackable)
    {
        if (trackable == null || trackable != trackedQr)
        {
            return;
        }

        Debug.Log(
            $"[PaperQrTracker] QR removed. " +
            $"Payload='{trackable.MarkerPayloadString}'",
            trackable
        );

        if (paperRoot != null)
        {
            // 제거된 Trackable 아래에 PaperRoot가 남는 것을 방지합니다.
            paperRoot.SetParent(transform, true);
            paperRoot.gameObject.SetActive(false);
        }

        trackedQr = null;
    }

    private void OnDestroy()
    {
        if (!listenersRegistered || mruk == null)
        {
            return;
        }

        mruk.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
        mruk.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
    }
}