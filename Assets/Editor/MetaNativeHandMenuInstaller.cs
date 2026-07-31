using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class MetaNativeHandMenuInstaller
{
    private const string TargetScenePath = "Assets/Scenes_Test/TestScene.unity";
    private const string PokeCanvasTemplateGuid =
        "4db41829582c7d24f80ee9603868dd67";

    static MetaNativeHandMenuInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/ChaekJaGuk/Install Meta Native Hand Menu")]
    private static void InstallFromMenu()
    {
        Install(forceLog: true);
    }

    private static void InstallIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Install(forceLog: false);
    }

    private static void Install(bool forceLog)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScenePath)
        {
            scene = EditorSceneManager.OpenScene(
                TargetScenePath,
                OpenSceneMode.Single);
        }

        var canvas = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "Hand Menu Canvas");
        if (canvas == null)
        {
            Debug.LogError(
                "Meta-native hand menu setup failed: Hand Menu Canvas was not found.");
            return;
        }

        var changed = false;
        var controller = canvas.GetComponent<MetaHandMenuController>();
        if (controller != null)
        {
            var serializedController = new SerializedObject(controller);
            var enablePoke =
                serializedController.FindProperty("enablePokeInteraction");
            if (enablePoke != null && enablePoke.boolValue)
            {
                enablePoke.boolValue = false;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }

        var eventSystem = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
            changed = true;
        }

        var canvasModule =
            eventSystem.GetComponent<PointableCanvasModule>();
        if (canvasModule == null)
        {
            canvasModule =
                eventSystem.gameObject.AddComponent<PointableCanvasModule>();
            changed = true;
        }

        // Unity's EventSystem processes one BaseInputModule at a time. This scene also
        // contains XRUIInputModule, so non-exclusive mode leaves Meta poke events queued
        // without PointableCanvasModule.Process() ever forwarding them to UGUI.
        if (!canvasModule.ExclusiveMode)
        {
            canvasModule.ExclusiveMode = true;
            changed = true;
        }

        var pointableCanvas =
            canvas.GetComponentInChildren<PointableCanvas>(true);
        if (pointableCanvas == null)
        {
            var prefabPath =
                AssetDatabase.GUIDToAssetPath(PokeCanvasTemplateGuid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = PrefabUtility.InstantiatePrefab(
                    prefab,
                    canvas.transform) as GameObject;

            instance.name = "[Meta] Poke Canvas Interaction";
            var rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.localPosition = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            pointableCanvas = instance.GetComponent<PointableCanvas>();
            changed = true;
        }

        pointableCanvas.InjectCanvas(canvas);
        EditorUtility.SetDirty(pointableCanvas);

        if (canvas.GetComponent<MetaPokeGlowBinder>() == null)
        {
            canvas.gameObject.AddComponent<MetaPokeGlowBinder>();
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (changed || forceLog)
        {
            Debug.Log(
                "Meta-native Hand Menu installed: PokeInteractor -> " +
                "PokeInteractable -> PointableCanvas, with " +
                "HandPokeOvershootGlow binding.",
                canvas);
        }
    }
}
