using Meta.XR;
using UnityEngine;

public class MemoPlacement : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private OVRInput.Button placeButton = OVRInput.Button.One;
    [SerializeField]
    private OVRInput.Controller controller =
        OVRInput.Controller.RTouch;

    [Header("Raycast")]
    [SerializeField] private Transform rayStartPoint;
    [SerializeField] private Transform headTransform;
    [SerializeField] private EnvironmentRaycastManager environmentRaycastManager;
    [SerializeField] private float rayLength = 5f;

    [Header("메모지")]
    [SerializeField] private MemoNote memoPrefab;
    [SerializeField] private float surfaceOffset = 0.001f;

    [Header("평평함 판정")]
    [SerializeField, Range(0f, 45f)]
    private float maxSlopeDegrees = 45f;

    private void Update()
    {
        // Raycast Collision 씬에서 실제로 동작하던 입력 경로를 그대로 사용합니다.
        // 특정 컨트롤러 마스크만 강제하면 현재 Active 컨트롤러가 Touch로
        // 보고되는 프레임에서 A 입력을 놓칠 수 있습니다.
        bool placePressed = OVRInput.GetDown(placeButton);

        // Active 컨트롤러 전환 시점에도 입력을 놓치지 않도록 오른쪽
        // 컨트롤러 검사도 보조적으로 유지합니다.
        placePressed |= OVRInput.GetDown(placeButton, controller);

        if (!placePressed)
        {
            return;
        }

        Debug.Log(
            $"[MemoPlacement] A 입력 감지. Active={OVRInput.GetActiveController()}, " +
            $"Connected={OVRInput.GetConnectedControllers()}",
            this
        );

        TryPlaceMemo();
    }

    private void TryPlaceMemo()
    {
        if (rayStartPoint == null ||
            environmentRaycastManager == null ||
            memoPrefab == null)
        {
            Debug.LogError("MemoPlacement의 Inspector 연결을 확인하세요.", this);
            return;
        }

        Ray ray = new Ray(
            rayStartPoint.position,
            rayStartPoint.forward
        );

        bool hasHit = environmentRaycastManager.Raycast(
            ray,
            out EnvironmentRaycastHit hit,
            rayLength
        );

        if (!hasHit)
        {
            Debug.LogWarning(
                $"[MemoPlacement] 환경 Raycast 실패: {hit.status}. " +
                $"origin={ray.origin}, direction={ray.direction}",
                this
            );
            return;
        }

        if (hit.normal.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning(
                $"[MemoPlacement] 표면 법선을 얻지 못했습니다. " +
                $"status={hit.status}, confidence={hit.normalConfidence:0.00}",
                this
            );
            return;
        }

        Vector3 surfaceNormal = hit.normal.normalized;

        // 0도면 완전한 수평면, 값이 커질수록 기울어진 면도 허용합니다.
        // Meta 공식 Instant Content Placement 샘플은 수평면을 upDot > 0.7
        // (약 45도)로 분류합니다. 기존 씬에 20도가 직렬화되어 있더라도
        // 깊이 법선 노이즈 때문에 모든 표면이 탈락하지 않도록 보정합니다.
        float effectiveMaxSlopeDegrees =
            Mathf.Max(maxSlopeDegrees, 45f);

        float minimumUpDot =
            Mathf.Cos(effectiveMaxSlopeDegrees * Mathf.Deg2Rad);

        float upDot =
            Vector3.Dot(surfaceNormal, Vector3.up);

        if (upDot < minimumUpDot)
        {
            Debug.LogWarning(
                $"[MemoPlacement] 수평면 판정 실패. " +
                $"upDot={upDot:0.00}, required={minimumUpDot:0.00}, " +
                $"normal={surfaceNormal}, confidence={hit.normalConfidence:0.00}",
                this
            );
            return;
        }

        Vector3 memoUp = GetMemoUpDirection(surfaceNormal);

        Quaternion memoRotation = Quaternion.LookRotation(
            surfaceNormal,
            memoUp
        );

        Vector3 memoPosition =
            hit.point + surfaceNormal * surfaceOffset;

        MemoNote spawnedMemo = Instantiate(
            memoPrefab,
            memoPosition,
            memoRotation
        );

        // 비활성 씬 인스턴스가 잘못 연결되어 있더라도 생성 결과가 보이도록
        // 방어적으로 활성화합니다. Project 프리팹을 연결한 현재 상태에서도
        // 부작용은 없습니다.
        spawnedMemo.gameObject.SetActive(true);

        Debug.Log(
            $"[MemoPlacement] 메모 생성 성공: {memoPosition}, " +
            $"normal={surfaceNormal}, confidence={hit.normalConfidence:0.00}",
            spawnedMemo
        );
    }

    private Vector3 GetMemoUpDirection(Vector3 surfaceNormal)
    {
        Vector3 referenceForward =
            headTransform != null
                ? headTransform.forward
                : Vector3.forward;

        Vector3 projectedForward =
            Vector3.ProjectOnPlane(referenceForward, surfaceNormal);

        if (projectedForward.sqrMagnitude < 0.001f)
        {
            projectedForward =
                Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);
        }

        if (projectedForward.sqrMagnitude < 0.001f)
        {
            projectedForward =
                Vector3.ProjectOnPlane(Vector3.right, surfaceNormal);
        }

        return projectedForward.normalized;
    }
}
