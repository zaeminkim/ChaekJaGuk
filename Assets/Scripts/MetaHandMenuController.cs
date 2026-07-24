using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class MetaHandMenuController : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform head;
    [SerializeField, Min(0f)] private float palmNormalOffset = 0.055f;
    [SerializeField, Min(0f)] private float fingerDirectionOffset = 0.055f;
    [SerializeField] private float outwardOffset = 0.04f;
    [SerializeField, Min(0.01f)] private float positionFollowSpeed = 18f;
    [SerializeField, Min(0.01f)] private float rotationFollowSpeed = 18f;
    [SerializeField] private bool invertPalmNormal;

    [Header("Visibility")]
    [SerializeField, Range(0f, 180f)] private float palmUpAngle = 75f;
    [SerializeField, Range(0f, 180f)] private float palmFacingUserAngle = 82f;
    [SerializeField, Range(0f, 180f)] private float gazeDivergenceAngle = 55f;
    [SerializeField, Range(0f, 45f)] private float visibilityHysteresis = 12f;
    [SerializeField, Min(0.01f)] private float revealSpeed = 12f;

    [Header("Right Index Poke")]
    [SerializeField] private bool enablePokeInteraction = true;
    [SerializeField, Min(0.001f)] private float pokeHoverDistance = 0.055f;
    [SerializeField, Min(0.001f)] private float pokePressDistance = 0.018f;
    [SerializeField, Min(0.001f)] private float pokeReleaseDistance = 0.035f;
    [SerializeField, Min(0f)] private float pokeBackTolerance = 0.012f;
    [SerializeField] private int pointerId = -101;

    private readonly List<RaycastResult> _raycastResults = new();

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private GraphicRaycaster _graphicRaycaster;
    private RectTransform _menuRect;
    private Camera _uiCamera;
    private Vector3 _shownScale;

    private OVRSkeleton _leftSkeleton;
    private OVRSkeleton _rightSkeleton;
    private Transform _leftWrist;
    private Transform _leftIndexBase;
    private Transform _leftLittleBase;
    private Transform _leftPalm;
    private Transform _rightIndexTip;

    private EventSystem _eventSystem;
    private PointerEventData _pointerData;
    private GameObject _hoveredObject;
    private GameObject _pressedObject;
    private bool _pointerPressed;

    private bool _menuVisible;
    private bool _hasFollowPose;
    private float _visibility;
    private float _nextDiscoveryTime;
    private Vector2 _lastPointerPosition;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _menuRect = transform as RectTransform;
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _shownScale = transform.localScale;
        transform.localScale = Vector3.zero;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void Start()
    {
        DiscoverRuntimeReferences();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextDiscoveryTime &&
            (_leftSkeleton == null || _rightSkeleton == null || head == null))
        {
            DiscoverRuntimeReferences();
        }

        var hasPalmFrame = TryGetLeftPalmFrame(
            out var palmPosition,
            out var palmNormal,
            out var fingerDirection,
            out var outwardDirection);

        if (hasPalmFrame)
        {
            UpdateMenuPose(palmPosition, palmNormal, fingerDirection, outwardDirection);
            _menuVisible = ShouldShowMenu(palmPosition, palmNormal);
        }
        else
        {
            _menuVisible = false;
            _hasFollowPose = false;
        }

        UpdateVisibility();

        if (enablePokeInteraction && _visibility > 0.9f)
        {
            UpdateRightIndexPoke();
        }
        else
        {
            CancelPointer();
        }
    }

    private void OnDisable()
    {
        CancelPointer();
    }

    private void DiscoverRuntimeReferences()
    {
        _nextDiscoveryTime = Time.unscaledTime + 1f;

        if (head == null)
        {
            _uiCamera = Camera.main;
            if (_uiCamera != null)
            {
                head = _uiCamera.transform;
            }
        }
        else if (_uiCamera == null)
        {
            _uiCamera = head.GetComponent<Camera>();
            if (_uiCamera == null)
            {
                _uiCamera = Camera.main;
            }
        }

        _eventSystem = EventSystem.current;
        if (_eventSystem != null && _pointerData == null)
        {
            _pointerData = new PointerEventData(_eventSystem)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left
            };
        }

        var skeletons = FindObjectsByType<OVRSkeleton>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var skeleton in skeletons)
        {
            switch (skeleton.GetSkeletonType())
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.XRHandLeft:
                    _leftSkeleton = skeleton;
                    break;
                case OVRSkeleton.SkeletonType.HandRight:
                case OVRSkeleton.SkeletonType.XRHandRight:
                    _rightSkeleton = skeleton;
                    break;
            }
        }

        CacheLeftHandBones();
        CacheRightHandBones();
    }

    private void CacheLeftHandBones()
    {
        if (_leftSkeleton == null || !_leftSkeleton.IsInitialized)
        {
            return;
        }

        var openXrBones = _leftSkeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandLeft;
        _leftWrist = FindBone(
            _leftSkeleton,
            openXrBones ? OVRSkeleton.BoneId.XRHand_Wrist : OVRSkeleton.BoneId.Hand_WristRoot);
        _leftPalm = openXrBones
            ? FindBone(_leftSkeleton, OVRSkeleton.BoneId.XRHand_Palm)
            : null;
        _leftIndexBase = FindBone(
            _leftSkeleton,
            openXrBones ? OVRSkeleton.BoneId.XRHand_IndexProximal : OVRSkeleton.BoneId.Hand_Index1);
        _leftLittleBase = FindBone(
            _leftSkeleton,
            openXrBones ? OVRSkeleton.BoneId.XRHand_LittleProximal : OVRSkeleton.BoneId.Hand_Pinky1);
    }

    private void CacheRightHandBones()
    {
        if (_rightSkeleton == null || !_rightSkeleton.IsInitialized)
        {
            return;
        }

        var openXrBones = _rightSkeleton.GetSkeletonType() == OVRSkeleton.SkeletonType.XRHandRight;
        _rightIndexTip = FindBone(
            _rightSkeleton,
            openXrBones ? OVRSkeleton.BoneId.XRHand_IndexTip : OVRSkeleton.BoneId.Hand_IndexTip);
    }

    private static Transform FindBone(OVRSkeleton skeleton, OVRSkeleton.BoneId id)
    {
        if (skeleton.Bones == null)
        {
            return null;
        }

        foreach (var bone in skeleton.Bones)
        {
            if (bone != null && bone.Id == id)
            {
                return bone.Transform;
            }
        }

        return null;
    }

    private bool TryGetLeftPalmFrame(
        out Vector3 palmPosition,
        out Vector3 palmNormal,
        out Vector3 fingerDirection,
        out Vector3 outwardDirection)
    {
        palmPosition = default;
        palmNormal = default;
        fingerDirection = default;
        outwardDirection = default;

        if (_leftSkeleton == null || !_leftSkeleton.IsInitialized || !_leftSkeleton.IsDataValid)
        {
            return false;
        }

        if (_leftWrist == null || _leftIndexBase == null || _leftLittleBase == null)
        {
            CacheLeftHandBones();
        }

        if (_leftWrist == null || _leftIndexBase == null || _leftLittleBase == null)
        {
            return false;
        }

        var fingerCenter = (_leftIndexBase.position + _leftLittleBase.position) * 0.5f;
        fingerDirection = (fingerCenter - _leftWrist.position).normalized;
        outwardDirection = (_leftLittleBase.position - _leftIndexBase.position).normalized;
        palmNormal = Vector3.Cross(outwardDirection, fingerDirection).normalized;

        if (invertPalmNormal)
        {
            palmNormal = -palmNormal;
        }

        palmPosition = _leftPalm != null ? _leftPalm.position : fingerCenter;
        return fingerDirection.sqrMagnitude > 0.9f &&
               outwardDirection.sqrMagnitude > 0.9f &&
               palmNormal.sqrMagnitude > 0.9f;
    }

    private void UpdateMenuPose(
        Vector3 palmPosition,
        Vector3 palmNormal,
        Vector3 fingerDirection,
        Vector3 outwardDirection)
    {
        var targetPosition = palmPosition +
                             palmNormal * palmNormalOffset +
                             fingerDirection * fingerDirectionOffset +
                             outwardDirection * outwardOffset;

        var toHead = head != null
            ? head.position - targetPosition
            : palmNormal;
        if (toHead.sqrMagnitude < 0.0001f)
        {
            toHead = palmNormal;
        }

        // A World Space Canvas considers its local -Z side to be the visible front.
        var targetRotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
        var positionBlend = 1f - Mathf.Exp(-positionFollowSpeed * Time.unscaledDeltaTime);
        var rotationBlend = 1f - Mathf.Exp(-rotationFollowSpeed * Time.unscaledDeltaTime);

        if (!_hasFollowPose)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            _hasFollowPose = true;
            return;
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, positionBlend);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
    }

    private bool ShouldShowMenu(Vector3 palmPosition, Vector3 palmNormal)
    {
        if (head == null)
        {
            return false;
        }

        var extraAngle = _menuVisible ? visibilityHysteresis : 0f;
        var upThreshold = Mathf.Cos((palmUpAngle + extraAngle) * Mathf.Deg2Rad);
        var userThreshold = Mathf.Cos((palmFacingUserAngle + extraAngle) * Mathf.Deg2Rad);
        var toUser = (head.position - palmPosition).normalized;

        if (Vector3.Dot(palmNormal, Vector3.up) < upThreshold ||
            Vector3.Dot(palmNormal, toUser) < userThreshold)
        {
            return false;
        }

        var gazeAngle = Vector3.Angle(head.forward, palmPosition - head.position);
        return gazeAngle <= gazeDivergenceAngle + extraAngle;
    }

    private void UpdateVisibility()
    {
        var targetVisibility = _menuVisible ? 1f : 0f;
        _visibility = Mathf.MoveTowards(
            _visibility,
            targetVisibility,
            revealSpeed * Time.unscaledDeltaTime);

        var easedVisibility = Mathf.SmoothStep(0f, 1f, _visibility);
        transform.localScale = Vector3.LerpUnclamped(Vector3.zero, _shownScale, easedVisibility);
        _canvasGroup.alpha = easedVisibility;

        var interactive = _visibility > 0.9f;
        _canvasGroup.interactable = interactive;
        _canvasGroup.blocksRaycasts = interactive;
    }

    private void UpdateRightIndexPoke()
    {
        if (_rightSkeleton == null || !_rightSkeleton.IsInitialized || !_rightSkeleton.IsDataValid)
        {
            CancelPointer();
            return;
        }

        if (_rightIndexTip == null)
        {
            CacheRightHandBones();
        }

        if (_rightIndexTip == null || _uiCamera == null || _eventSystem == null ||
            _pointerData == null || _graphicRaycaster == null || _menuRect == null)
        {
            CancelPointer();
            return;
        }

        var tipPosition = _rightIndexTip.position;
        var screenPosition3 = _uiCamera.WorldToScreenPoint(tipPosition);
        if (screenPosition3.z <= 0f)
        {
            CancelPointer();
            return;
        }

        var screenPosition = new Vector2(screenPosition3.x, screenPosition3.y);
        _pointerData.delta = screenPosition - _lastPointerPosition;
        _pointerData.position = screenPosition;
        _lastPointerPosition = screenPosition;

        var panelFrontNormal = -transform.forward;
        var distanceToFront = Vector3.Dot(tipPosition - transform.position, panelFrontNormal);
        var inFrontOfPanel = distanceToFront >= -pokeBackTolerance;
        var withinHoverDistance = distanceToFront <= pokeHoverDistance;
        var insideRect = RectTransformUtility.RectangleContainsScreenPoint(
            _menuRect,
            screenPosition,
            _uiCamera);

        GameObject hitObject = null;
        _raycastResults.Clear();
        if (inFrontOfPanel && withinHoverDistance && insideRect)
        {
            _graphicRaycaster.Raycast(_pointerData, _raycastResults);
            if (_raycastResults.Count > 0)
            {
                _pointerData.pointerCurrentRaycast = _raycastResults[0];
                hitObject = _raycastResults[0].gameObject;
            }
        }

        SetHoveredObject(hitObject);

        if (!_pointerPressed &&
            hitObject != null &&
            distanceToFront <= pokePressDistance)
        {
            PressPointer(hitObject);
        }

        if (_pointerPressed)
        {
            if (_pointerData.pointerDrag != null)
            {
                ExecuteEvents.Execute(
                    _pointerData.pointerDrag,
                    _pointerData,
                    ExecuteEvents.dragHandler);
            }

            if (distanceToFront >= pokeReleaseDistance ||
                distanceToFront < -pokeBackTolerance ||
                !insideRect)
            {
                ReleasePointer(hitObject, true);
            }
        }
    }

    private void SetHoveredObject(GameObject hitObject)
    {
        var nextHoveredObject = hitObject != null
            ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hitObject)
            : null;

        if (_hoveredObject == nextHoveredObject)
        {
            return;
        }

        if (_hoveredObject != null)
        {
            ExecuteEvents.Execute(
                _hoveredObject,
                _pointerData,
                ExecuteEvents.pointerExitHandler);
        }

        _hoveredObject = nextHoveredObject;
        _pointerData.pointerEnter = _hoveredObject;

        if (_hoveredObject != null)
        {
            ExecuteEvents.Execute(
                _hoveredObject,
                _pointerData,
                ExecuteEvents.pointerEnterHandler);
        }
    }

    private void PressPointer(GameObject hitObject)
    {
        _pointerPressed = true;
        _pointerData.eligibleForClick = true;
        _pointerData.pressPosition = _pointerData.position;
        _pointerData.pointerPressRaycast = _pointerData.pointerCurrentRaycast;
        _pointerData.rawPointerPress = hitObject;

        _pressedObject = ExecuteEvents.ExecuteHierarchy(
            hitObject,
            _pointerData,
            ExecuteEvents.pointerDownHandler);

        if (_pressedObject == null)
        {
            _pressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
        }

        _pointerData.pointerPress = _pressedObject;
        _pointerData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(hitObject);

        if (_pointerData.pointerDrag != null)
        {
            ExecuteEvents.Execute(
                _pointerData.pointerDrag,
                _pointerData,
                ExecuteEvents.initializePotentialDrag);
        }
    }

    private void ReleasePointer(GameObject hitObject, bool allowClick)
    {
        if (!_pointerPressed || _pointerData == null)
        {
            return;
        }

        if (_pressedObject != null)
        {
            ExecuteEvents.Execute(
                _pressedObject,
                _pointerData,
                ExecuteEvents.pointerUpHandler);
        }

        if (allowClick && _pointerData.eligibleForClick && hitObject != null)
        {
            var releaseClickHandler =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
            var pressClickHandler =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(_pressedObject);

            if (releaseClickHandler != null && releaseClickHandler == pressClickHandler)
            {
                ExecuteEvents.Execute(
                    releaseClickHandler,
                    _pointerData,
                    ExecuteEvents.pointerClickHandler);
            }
        }

        if (_pointerData.pointerDrag != null)
        {
            ExecuteEvents.Execute(
                _pointerData.pointerDrag,
                _pointerData,
                ExecuteEvents.endDragHandler);
        }

        _pointerPressed = false;
        _pointerData.eligibleForClick = false;
        _pointerData.pointerPress = null;
        _pointerData.rawPointerPress = null;
        _pointerData.pointerDrag = null;
        _pressedObject = null;
    }

    private void CancelPointer()
    {
        ReleasePointer(null, false);

        if (_hoveredObject != null && _pointerData != null)
        {
            ExecuteEvents.Execute(
                _hoveredObject,
                _pointerData,
                ExecuteEvents.pointerExitHandler);
        }

        _hoveredObject = null;
        if (_pointerData != null)
        {
            _pointerData.pointerEnter = null;
        }
    }
}
