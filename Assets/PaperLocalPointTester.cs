using UnityEngine;

public sealed class PaperLocalPointTester : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private OVRInput.Button placeButton = OVRInput.Button.One;

    [SerializeField]
    private OVRInput.Controller controller =
        OVRInput.Controller.RTouch;

    [Header("Ray")]
    [SerializeField] private Transform rayStartPoint;
    [SerializeField] private LayerMask paperLayer;
    [SerializeField] private float rayLength = 5f;

    [Header("Paper")]
    [SerializeField] private Transform paperRoot;
    [SerializeField] private Transform debugPointsRoot;

    [SerializeField]
    private Vector2 paperSizeMeters = new Vector2(0.210f, 0.297f);

    [SerializeField] private float markerDiameter = 0.006f;
    [SerializeField] private float surfaceOffset = 0.002f;

    private void Update()
    {
        bool pressed = OVRInput.GetDown(placeButton);
        pressed |= OVRInput.GetDown(placeButton, controller);

        if (!pressed)
        {
            return;
        }

        TryPlaceLocalPoint();
    }

    private void TryPlaceLocalPoint()
    {
        if (rayStartPoint == null || paperRoot == null)
        {
            Debug.LogError(
                "[PaperLocalPointTester] Inspector 연결을 확인하세요.",
                this
            );
            return;
        }

        Ray ray = new Ray(
            rayStartPoint.position,
            rayStartPoint.forward
        );

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            rayLength,
            paperLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!hasHit)
        {
            Debug.LogWarning(
                "[PaperLocalPointTester] PaperSurface Raycast 실패.",
                this
            );
            return;
        }

        Vector3 markerWorldPosition =
            hit.point + hit.normal * surfaceOffset;

        Vector3 localHit =
            paperRoot.InverseTransformPoint(hit.point);

        float u =
            localHit.x / paperSizeMeters.x + 0.5f;

        float v =
            0.5f - localHit.y / paperSizeMeters.y;

        GameObject marker =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        marker.name = $"PaperPoint_U{u:F3}_V{v:F3}";
        marker.transform.position = markerWorldPosition;
        marker.transform.localScale =
            Vector3.one * markerDiameter;

        Transform markerParent =
            debugPointsRoot != null
                ? debugPointsRoot
                : paperRoot;

        marker.transform.SetParent(markerParent, true);

        Collider markerCollider =
            marker.GetComponent<Collider>();

        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        Renderer markerRenderer =
            marker.GetComponent<Renderer>();

        if (markerRenderer != null)
        {
            markerRenderer.material.color = Color.red;
        }

        Debug.Log(
            $"[PaperLocalPointTester] " +
            $"Local={localHit}, UV=({u:F3}, {v:F3})",
            marker
        );
    }
}