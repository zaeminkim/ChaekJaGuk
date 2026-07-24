using System.Collections;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MetaPokeGlowBinder : MonoBehaviour
{
    [SerializeField]
    private Color glowColor = new(0.25f, 1f, 0.2f, 1f);

    [SerializeField, Min(0.001f)]
    private float overshootMaxDistance = 0.15f;

    [SerializeField]
    private HandPokeOvershootGlow.GlowType glowType =
        HandPokeOvershootGlow.GlowType.Outline;

    [SerializeField, Min(1)]
    private int discoveryFrames = 120;

    private IEnumerator Start()
    {
        for (var frame = 0; frame < discoveryFrames; frame++)
        {
            if (ConfigureMetaGlow())
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "Meta poke glow could not find a compatible Meta hand visual. " +
            "The Poke Canvas remains functional, but the overshoot glow is unavailable.",
            this);
    }

    private bool ConfigureMetaGlow()
    {
        var glows = FindObjectsByType<HandPokeOvershootGlow>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var visuals = FindObjectsByType<HandVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var configuredAny = false;

        foreach (var glow in glows)
        {
            var pokeInteractor = glow.GetComponentInParent<PokeInteractor>(true);
            var handRef = glow.GetComponentInParent<HandRef>(true);
            if (pokeInteractor == null || handRef == null || handRef.Hand == null)
            {
                continue;
            }

            var handVisual = FindHandVisual(visuals, handRef);
            if (handVisual == null)
            {
                continue;
            }

            var renderer = FindGlowRenderer(handVisual);
            var materialEditor = FindMaterialEditor(handVisual, renderer);
            if (renderer == null || materialEditor == null)
            {
                continue;
            }

            glow.gameObject.SetActive(false);
            glow.InjectAllHandPokeOvershootGlow(
                handRef,
                pokeInteractor,
                handVisual,
                renderer,
                materialEditor);
            glow.InjectGlowColor(glowColor);
            glow.InjectOvershootMaxDistance(overshootMaxDistance);
            glow.InjectGlowType(glowType);
            glow.gameObject.SetActive(true);

            StartCoroutine(RefreshSubscription(glow));
            configuredAny = true;
        }

        return configuredAny;
    }

    private static HandVisual FindHandVisual(
        HandVisual[] visuals,
        HandRef handRef)
    {
        foreach (var visual in visuals)
        {
            if (visual.Hand != null &&
                visual.Hand.Handedness == handRef.Handedness)
            {
                return visual;
            }
        }

        var handName = handRef.Handedness == Handedness.Left
            ? "left"
            : "right";

        return visuals.FirstOrDefault(
            visual => GetPath(visual.transform).ToLowerInvariant().Contains(handName));
    }

    private static SkinnedMeshRenderer FindGlowRenderer(HandVisual handVisual)
    {
        var renderers = handVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterials.Any(
                    material => material != null &&
                                material.HasProperty("_GenerateGlow")))
            {
                return renderer;
            }
        }

        return renderers.FirstOrDefault();
    }

    private static MaterialPropertyBlockEditor FindMaterialEditor(
        HandVisual handVisual,
        SkinnedMeshRenderer renderer)
    {
        var editors =
            handVisual.GetComponentsInChildren<MaterialPropertyBlockEditor>(true);

        foreach (var editor in editors)
        {
            if (editor.Renderers != null && editor.Renderers.Contains(renderer))
            {
                return editor;
            }
        }

        var editorOnRenderer =
            renderer.GetComponent<MaterialPropertyBlockEditor>();
        return editorOnRenderer != null
            ? editorOnRenderer
            : editors.FirstOrDefault();
    }

    private static IEnumerator RefreshSubscription(
        HandPokeOvershootGlow glow)
    {
        yield return null;
        glow.enabled = false;
        glow.enabled = true;
    }

    private static string GetPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
