using UnityEngine;

public class MemoNote : MonoBehaviour
{
    [SerializeField] private Transform strokeRoot;

    public Transform StrokeRoot
    {
        get
        {
            return strokeRoot != null ? strokeRoot : transform;
        }
    }
}