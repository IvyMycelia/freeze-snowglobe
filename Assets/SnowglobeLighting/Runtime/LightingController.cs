using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Smoothly blends one Global Light 2D, local lights, fog and URP Volume settings.
/// Keep the sunbeam/corruption arrays distinct; do not include a light in both.
/// </summary>
public sealed class SnowglobeLightingController : MonoBehaviour
{
    [Header("Profiles")]
    public SnowglobeLightingProfile livingProfile;
    public SnowglobeLightingProfile corruptedProfile;

    [Header("Scene references")]
    public Light2D ambientGlobalLight;
    public Light2D[] sunbeams;
    public Light2D[] corruptionGlows;
    public SpriteRenderer[] fogLayers;
    public Volume postProcessVolume;

    [Header("Transition")]
    [Range(0f, 1f)] public float startingCorruption;
    [Min(0.01f)] public float transitionSeconds = 2.5f;

    public float CurrentCorruption => currentCorruption;

    private float currentCorruption;
    private float targetCorruption;
    private Bloom bloom;
    private ColorAdjustments colorAdjustments;
    private bool volumePrepared;

    private void Start()
    {
        currentCorruption = Mathf.Clamp01(startingCorruption);
        targetCorruption = currentCorruption;
        PrepareVolume();
        Apply(currentCorruption);
    }

    private void Update()
    {
        if (livingProfile == null || corruptedProfile == null) return;
        if (Mathf.Abs(currentCorruption - targetCorruption) < 0.0001f) return;

        currentCorruption = Mathf.MoveTowards(
            currentCorruption, targetCorruption,
            Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));

        // Smooth visually without changing the underlying progress variable.
        Apply(Mathf.SmoothStep(0f, 1f, currentCorruption));
    }

    public void TransitionTo(float target)
    {
        targetCorruption = Mathf.Clamp01(target);
    }

    public void SetImmediately(float value)
    {
        currentCorruption = Mathf.Clamp01(value);
        targetCorruption = currentCorruption;
        if (!volumePrepared) PrepareVolume();
        Apply(Mathf.SmoothStep(0f, 1f, currentCorruption));
    }

    public void RestoreForest() => TransitionTo(0f);
    public void CorruptForest() => TransitionTo(1f);

    private void PrepareVolume()
    {
        volumePrepared = true;
        if (postProcessVolume == null) return;

        // Volume.profile creates a runtime copy, leaving the editor asset intact.
        VolumeProfile runtimeProfile = postProcessVolume.profile;
        if (runtimeProfile == null) return;

        runtimeProfile.TryGet(out bloom);
        runtimeProfile.TryGet(out colorAdjustments);
    }

    private void Apply(float t)
    {
        if (livingProfile == null || corruptedProfile == null) return;

        if (ambientGlobalLight != null)
        {
            ambientGlobalLight.color = Color.Lerp(
                livingProfile.ambientColor, corruptedProfile.ambientColor, t);
            ambientGlobalLight.intensity = Mathf.Lerp(
                livingProfile.ambientIntensity, corruptedProfile.ambientIntensity, t);
        }

        if (sunbeams != null)
        {
            foreach (Light2D light2D in sunbeams)
            {
                if (light2D == null) continue;
                light2D.color = Color.Lerp(
                    livingProfile.sunlightColor, corruptedProfile.sunlightColor, t);
                light2D.intensity = Mathf.Lerp(
                    livingProfile.sunlightIntensity, corruptedProfile.sunlightIntensity, t);
            }
        }

        if (corruptionGlows != null)
        {
            foreach (Light2D light2D in corruptionGlows)
            {
                if (light2D == null) continue;
                light2D.color = Color.Lerp(
                    livingProfile.corruptionGlowColor, corruptedProfile.corruptionGlowColor, t);
                light2D.intensity = Mathf.Lerp(
                    livingProfile.corruptionGlowIntensity,
                    corruptedProfile.corruptionGlowIntensity, t);
            }
        }

        if (fogLayers != null)
        {
            Color tint = Color.Lerp(livingProfile.fogColor, corruptedProfile.fogColor, t);
            float opacity = Mathf.Lerp(livingProfile.fogOpacity,
                corruptedProfile.fogOpacity, t);
            foreach (SpriteRenderer renderer2D in fogLayers)
            {
                if (renderer2D == null) continue;
                renderer2D.color = new Color(tint.r, tint.g, tint.b, opacity);
            }
        }

        if (bloom != null)
            bloom.intensity.Override(Mathf.Lerp(
                livingProfile.bloomIntensity, corruptedProfile.bloomIntensity, t));

        if (colorAdjustments != null)
        {
            colorAdjustments.colorFilter.Override(Color.Lerp(
                livingProfile.colorFilter, corruptedProfile.colorFilter, t));
            colorAdjustments.contrast.Override(Mathf.Lerp(
                livingProfile.contrast, corruptedProfile.contrast, t));
            colorAdjustments.saturation.Override(Mathf.Lerp(
                livingProfile.saturation, corruptedProfile.saturation, t));
        }
    }
}
