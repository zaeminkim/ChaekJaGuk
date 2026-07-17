using UnityEngine;
using System.Collections;

public class PlaceCanvasInFrontOfHead : MonoBehaviour 
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform canvas;
    [SerializeField] private float distance = 1.2f;
    [SerializeField] private float heightOffset = 0f;
    [SerializeField] private float minTrackedHeadHeight = 0.6f;
    [SerializeField] private float maxWaitSeconds = 3f;

    private void Start()
    {
        StartCoroutine(PlaceWhenHeadTrackingIsReady());
    }

    private IEnumerator PlaceWhenHeadTrackingIsReady()
    {
        if (head == null || canvas == null)
        {
            Debug.LogWarning("Head 또는 Canvas가 연결되지 않았습니다.");
            yield break;
        }

        float startTime = Time.time;
        while (head.position.y < minTrackedHeadHeight && Time.time - startTime < maxWaitSeconds)
        {
            yield return null;
        }

        PlaceCanvas();
    }

    private void PlaceCanvas()
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        canvas.position = head.position + forward * distance + Vector3.up * heightOffset;
        canvas.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
