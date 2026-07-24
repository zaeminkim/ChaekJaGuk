using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XRHandDraw : MonoBehaviour
{
    [SerializeField] private GameObject trackingHand;

    [SerializeField] private float minFingerPinchStrength = 0.5f;

    [SerializeField] private float minDistanceBeforeNewPoint = 0.008f;

    [SerializeField] private float tubeDefaultWidth = 0.010f;
    [SerializeField] private int tubeSides = 8;

    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Material defaultLineMaterial;

    [SerializeField] private bool enableGravity = false;
    [SerializeField] private bool colliderTrigger = false;

    private Vector3 prevPointDistance = Vector3.zero;

    private List<Vector3> points = new List<Vector3>();
    private List<TubeRenderer> tubeRenderers = new List<TubeRenderer>();

    private TubeRenderer currentTubeRenderer;

    private bool isPinchingReleased = false;

    private OVRHand ovrHand;

    private OVRSkeleton ovrSkeleton;

    private Transform intexfinger;

    private void Start()
    {
        ovrHand = trackingHand.GetComponent<OVRHand>();
        ovrSkeleton = trackingHand.GetComponent<OVRSkeleton>();

        AddNewTubeRenderer();
    }

    private void Update()
    {
        foreach (var b in ovrSkeleton.Bones)
        {
            if (b.Id == OVRSkeleton.BoneId.XRHand_IndexTip)
            {
                intexfinger = b.Transform;
                break;
            }
        }

        CheckPinchState();
    }

    private void AddNewTubeRenderer()
    {
        points.Clear();
        GameObject go = new GameObject($"TubeRenderer__{tubeRenderers.Count}");
        go.transform.position = Vector3.zero;

        TubeRenderer goTubeRenderer = go.AddComponent<TubeRenderer>();
        tubeRenderers.Add(goTubeRenderer);

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.material = defaultLineMaterial;

        goTubeRenderer.ColliderTrigger = colliderTrigger;
        goTubeRenderer.SetPositions(points.ToArray());
        goTubeRenderer._radiusOne = tubeDefaultWidth;
        goTubeRenderer._radiusTwo = tubeDefaultWidth;
        goTubeRenderer._sides = tubeSides;

        currentTubeRenderer = goTubeRenderer;
    }

    private void CheckPinchState()
    {
        if (intexfinger == null)
            return;

        bool isIndexFingerPinching = ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        float indexFingerPinchStrength = ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

        if (ovrHand.GetFingerConfidence(OVRHand.HandFinger.Index) != OVRHand.TrackingConfidence.High)
            return;

        // finger pinch down
        if (isIndexFingerPinching && indexFingerPinchStrength >= minFingerPinchStrength)
        {
            UpdateTube();
            isPinchingReleased = true;
            return;
        }

        // finger pinch up
        if (isPinchingReleased)
        {
            if (enableGravity)
                currentTubeRenderer.EnableGravity();

            AddNewTubeRenderer();
            isPinchingReleased = false;
        }
    }

    private void UpdateTube()
    {
        if (prevPointDistance == Vector3.zero)
        {
            prevPointDistance = intexfinger.transform.position;
        }

        if (Vector3.Distance(prevPointDistance, intexfinger.transform.position) >= minDistanceBeforeNewPoint)
        {
            prevPointDistance = intexfinger.transform.position;
            AddPoint(prevPointDistance);
        }
    }

    private void AddPoint(Vector3 position)
    {
        points.Add(position);
        currentTubeRenderer.SetPositions(points.ToArray());
        currentTubeRenderer.GenerateMesh();
    }

    public void UpdateLineWidth(float newValue)
    {
        currentTubeRenderer._radiusOne = newValue;
        currentTubeRenderer._radiusTwo = newValue;
        tubeDefaultWidth = newValue;
    }

    public void UpdateLineColor(Color color)
    {
        defaultColor = color;
        defaultLineMaterial.color = color;
        currentTubeRenderer.material = defaultLineMaterial;
    }

    public void UpdateLineMinDistance(float newValue)
    {
        minDistanceBeforeNewPoint = newValue;
    }
}