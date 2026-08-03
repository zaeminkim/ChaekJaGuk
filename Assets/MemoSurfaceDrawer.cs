using UnityEngine;
using UnityEngine.Rendering;

public class MemoSurfaceDrawer : MonoBehaviour
{
    [Header("입력")]
    [SerializeField]
    private OVRInput.Controller controller =
        OVRInput.Controller.RTouch;

    [SerializeField]
    private OVRInput.Axis1D drawTrigger =
        OVRInput.Axis1D.PrimaryIndexTrigger;

    [SerializeField, Range(0f, 1f)]
    private float triggerThreshold = 0.2f;

    [Header("Raycast")]
    [SerializeField] private Transform rayStartPoint;
    [SerializeField] private LayerMask memoSurfaceLayer;
    [SerializeField] private float rayLength = 5f;

    [Header("선")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField] private float lineWidth = 0.003f;
    [SerializeField] private float minimumPointDistance = 0.001f;
    [SerializeField] private float surfaceOffset = 0.001f;

    [Header("획 안정화")]
    [SerializeField, Min(0f)] private float smoothingSpeed = 35f;
    [SerializeField, Min(0f)] private float rayMissGraceTime = 0.08f;

    private LineRenderer currentLine;
    private MemoNote currentMemo;
    private Vector3 previousLocalPoint;
    private float strokeLocalZ;
    private float lastValidHitTime;
    private bool wasDrawing;
    private Material runtimeLineMaterial;

    private void Awake()
    {
        CreateRuntimeLineMaterial();
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
        }
    }

    private void Update()
    {
        float triggerValue = OVRInput.Get(drawTrigger, controller);
        bool isDrawing = triggerValue >= triggerThreshold;

        if (!isDrawing)
        {
            wasDrawing = false;
            EndStroke();
            return;
        }

        bool hasDrawn = DrawAtCurrentRay();

        if (!hasDrawn && !wasDrawing)
        {
            Debug.Log(
                $"[MemoSurfaceDrawer] 그리기 입력은 감지했지만 메모 표면을 맞히지 못했습니다. " +
                $"trigger={triggerValue:0.00}",
                this
            );
        }

        wasDrawing = true;
    }

    private bool DrawAtCurrentRay()
    {
        if (rayStartPoint == null)
        {
            return false;
        }

        Ray ray = new Ray(
            rayStartPoint.position,
            rayStartPoint.forward
        );

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            rayLength,
            memoSurfaceLayer,
            QueryTriggerInteraction.Collide
        );

        if (!hasHit)
        {
            EndStrokeAfterGracePeriod();
            return false;
        }

        MemoNote hitMemo =
            hit.collider.GetComponentInParent<MemoNote>();

        if (hitMemo == null)
        {
            EndStrokeAfterGracePeriod();
            return false;
        }

        if (currentLine == null || currentMemo != hitMemo)
        {
            BeginStroke(hitMemo, hit);
        }

        lastValidHitTime = Time.time;

        // Collider와 선이 겹쳐 깜빡이지 않도록 살짝 앞으로 이동합니다.
        Vector3 worldPoint =
            hit.point + hit.normal * surfaceOffset;

        Vector3 localPoint =
            hitMemo.StrokeRoot.InverseTransformPoint(worldPoint);

        // 깊이 센서와 Collider 교차점의 미세한 Z 흔들림을 없애고 메모
        // 표면과 평행한 한 평면 위에 획을 유지합니다.
        localPoint.z = strokeLocalZ;

        if (currentLine.positionCount == 0)
        {
            // LineRenderer는 점 하나만으로는 아무것도 표시하지 않습니다.
            // 같은 점을 두 개 넣어 짧은 탭도 둥근 점으로 보이게 합니다.
            currentLine.positionCount = 2;
            currentLine.SetPosition(0, localPoint);
            currentLine.SetPosition(1, localPoint);
            previousLocalPoint = localPoint;
            return true;
        }

        float smoothingFactor = smoothingSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);

        Vector3 smoothedLocalPoint = Vector3.Lerp(
            previousLocalPoint,
            localPoint,
            smoothingFactor
        );

        smoothedLocalPoint.z = strokeLocalZ;

        float distance = Vector3.Distance(
            previousLocalPoint,
            smoothedLocalPoint
        );

        if (distance < minimumPointDistance)
        {
            return true;
        }

        int newIndex = currentLine.positionCount;
        currentLine.positionCount++;

        currentLine.SetPosition(newIndex, smoothedLocalPoint);
        previousLocalPoint = smoothedLocalPoint;
        return true;
    }

    private void BeginStroke(MemoNote memo, RaycastHit hit)
    {
        EndStroke();

        currentMemo = memo;
        lastValidHitTime = Time.time;

        Vector3 firstWorldPoint =
            hit.point + hit.normal * surfaceOffset;

        strokeLocalZ = memo.StrokeRoot
            .InverseTransformPoint(firstWorldPoint).z;

        GameObject strokeObject = new GameObject("Stroke");
        strokeObject.transform.SetParent(memo.StrokeRoot, false);

        currentLine = strokeObject.AddComponent<LineRenderer>();

        currentLine.useWorldSpace = false;
        currentLine.alignment = LineAlignment.TransformZ;
        currentLine.widthMultiplier = lineWidth;
        currentLine.startColor = lineColor;
        currentLine.endColor = lineColor;
        currentLine.numCornerVertices = 4;
        currentLine.numCapVertices = 4;
        currentLine.positionCount = 0;
        currentLine.shadowCastingMode = ShadowCastingMode.Off;
        currentLine.receiveShadows = false;
        currentLine.sortingOrder = 100;

        if (runtimeLineMaterial != null)
        {
            currentLine.sharedMaterial = runtimeLineMaterial;
        }
    }

    private void EndStrokeAfterGracePeriod()
    {
        if (currentLine != null &&
            Time.time - lastValidHitTime > rayMissGraceTime)
        {
            EndStroke();
        }
    }

    private void CreateRuntimeLineMaterial()
    {
        Shader unlitShader = Shader.Find(
            "Universal Render Pipeline/Unlit"
        );

        if (lineMaterial != null)
        {
            runtimeLineMaterial = new Material(lineMaterial);
        }
        else if (unlitShader != null)
        {
            runtimeLineMaterial = new Material(unlitShader);
        }

        if (runtimeLineMaterial == null)
        {
            return;
        }

        if (unlitShader != null)
        {
            runtimeLineMaterial.shader = unlitShader;
        }

        // 조명과 보는 방향에 영향받지 않는 양면 선으로 만듭니다.
        runtimeLineMaterial.SetColor("_BaseColor", Color.white);
        runtimeLineMaterial.SetColor("_Color", Color.white);
        runtimeLineMaterial.SetFloat("_Cull", (float)CullMode.Off);
        runtimeLineMaterial.SetFloat("_ZWrite", 0f);
        runtimeLineMaterial.renderQueue = (int)RenderQueue.Transparent;
    }

    private void EndStroke()
    {
        currentLine = null;
        currentMemo = null;
    }
}
