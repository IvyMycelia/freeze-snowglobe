using UnityEngine;

[CreateAssetMenu(fileName = "LightingProfile", menuName = "Snowglobe/Lighting Profile")]
public sealed class SnowglobeLightingProfile : ScriptableObject
{
    [Header("Global ambience")]
    public Color ambientColor = new Color32(255, 232, 196, 255);
    [Min(0f)] public float ambientIntensity = 0.78f;

    [Header("Sunlight")]
    public Color sunlightColor = new Color32(255, 225, 162, 255);
    [Min(0f)] public float sunlightIntensity = 0.90f;

    [Header("Corruption accent")]
    public Color corruptionGlowColor = new Color32(73, 224, 203, 255);
    [Min(0f)] public float corruptionGlowIntensity = 0f;

    [Header("Atmosphere overlay")]
    public Color fogColor = new Color32(165, 198, 174, 255);
    [Range(0f, 1f)] public float fogOpacity = 0.16f;

    [Header("Post-processing")]
    [Min(0f)] public float bloomIntensity = 0.12f;
    public Color colorFilter = Color.white;
    [Range(-100f, 100f)] public float contrast = 5f;
    [Range(-100f, 100f)] public float saturation = 0f;
}
