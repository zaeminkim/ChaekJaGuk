using System.Collections;
using System.IO;
using Meta.XR;
using UnityEngine;

public class PcaFrameProbe : MonoBehaviour
{
    [SerializeField] private PassthroughCameraAccess cameraAccess;

    private Texture2D snapshot;

    private IEnumerator Start()
    {
        if (cameraAccess == null)
        {
            Debug.LogError("[OCR] Camera Access가 연결되지 않았습니다.");
            yield break;
        }

        while (!cameraAccess.IsPlaying)
        {
            yield return null;
        }

        Vector2Int size = cameraAccess.CurrentResolution;
        Debug.Log($"[OCR] Camera ready: {size.x} x {size.y}");
        Debug.Log($"[OCR] Save folder: {Application.persistentDataPath}");
    }

    private void Update()
    {
        // 오른쪽 컨트롤러 B 버튼
        if (OVRInput.GetDown(
                OVRInput.Button.Two,
                OVRInput.Controller.RTouch))
        {
            SaveFullFrame();
        }
    }

    private void SaveFullFrame()
    {
        if (!cameraAccess.IsPlaying)
        {
            Debug.LogWarning("[OCR] Camera is not playing.");
            return;
        }

        Vector2Int size = cameraAccess.CurrentResolution;

        if (snapshot == null ||
            snapshot.width != size.x ||
            snapshot.height != size.y)
        {
            if (snapshot != null)
            {
                Destroy(snapshot);
            }

            snapshot = new Texture2D(
                size.x,
                size.y,
                TextureFormat.RGBA32,
                false);
        }

        var colors = cameraAccess.GetColors();

        snapshot.LoadRawTextureData(colors);
        snapshot.Apply(false, false);

        byte[] png = snapshot.EncodeToPNG();

        string path = Path.Combine(
            Application.persistentDataPath,
            "ocr_full.png");

        File.WriteAllBytes(path, png);

        Debug.Log($"[OCR] Full frame saved: {path}");
    }
}