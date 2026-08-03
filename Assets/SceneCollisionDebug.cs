using UnityEngine;
//using Meta.XR.MRUtilityKit;
using Meta.XR;

public class SceneCollisionDebug : MonoBehaviour
{
    public OVRInput.Button button = OVRInput.Button.One; // 오른쪽 컨트롤러 A버튼

    public Transform rayStartPoint; // 충돌감지를 위한 광선
    public float rayLength = 5; // 광선의 길이
    //public MRUKAnchor.SceneLabels labelFilter; // 어떤 라벨 요소와 충돌할지를 결정
    public EnvironmentRaycastManager envRayManager;

    public TMPro.TextMeshPro debugTextPrefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(OVRInput.GetDown(button))
        {
            Ray ray = new Ray(rayStartPoint.position, rayStartPoint.forward); // (시작점, 방향)
            //MRUKRoom room = MRUK.Instance.GetCurrentRoom();

            //bool hasHit = room.Raycast(ray, rayLength, new LabelFilter(labelFilter), out RaycastHit hit, out MRUKAnchor anchor);
            bool hasHit = envRayManager.Raycast(ray, out var hit, rayLength);

            if (hasHit)
            {
                TMPro.TextMeshPro debugText = Instantiate(debugTextPrefab);

                Vector3 hitpoint = hit.point;
                Vector3 hitNormal = hit.normal;

                debugText.transform.position = hitpoint;
                debugText.transform.rotation = Quaternion.LookRotation(-hitNormal);

                //debugText.text = "ANCHOR: " + anchor.Label.ToString();
                debugText.text = "HIT";
            }
        }
    }
}
