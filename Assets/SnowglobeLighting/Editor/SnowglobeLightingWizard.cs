#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SnowglobeLightingWizard
{
    private const string Base = "Assets/SnowglobeLighting";

    [MenuItem("Tools/Snowglobe/Create Forest Lighting Rig")]
    public static void CreateRig()
    {
        if (GameObject.Find("SnowglobeLighting") != null)
        {
            EditorUtility.DisplayDialog("Snowglobe Lighting",
                "A SnowglobeLighting object already exists. Delete it first if you wish to rebuild it. No changes were made.", "OK");
            return;
        }

        EnsureFolder(Base + "/Profiles");
        ConfigureTexture("FogLayer_Back.png", 64f);
        ConfigureTexture("FogLayer_Front.png", 64f);
        ConfigureTexture("Sunbeam_Soft.png", 64f);

        SnowglobeLightingProfile living = GetOrCreateProfile("LivingForest.asset", false);
        SnowglobeLightingProfile corrupted = GetOrCreateProfile("CorruptedForest.asset", true);
        VolumeProfile postProfile = GetOrCreateVolume();

        GameObject root = new GameObject("SnowglobeLighting");
        Undo.RegisterCreatedObjectUndo(root, "Create Snowglobe Lighting");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 anchor = player != null ? player.transform.position : Vector3.zero;

        Light2D global = FindExistingGlobal();
        if (global == null)
        {
            global = CreateLight("Global Light 2D", root.transform, Light2D.LightType.Global,
                new Vector3(anchor.x, anchor.y, 0f));
        }
        global.color = living.ambientColor;
        global.intensity = living.ambientIntensity;

        Light2D[] sunbeams = new Light2D[3];
        sunbeams[0] = CreateSpot("Sunbeam_01", root.transform,
            new Vector3(anchor.x - 5f, anchor.y + 7f, 0f), -55f, 9f, 36f);
        sunbeams[1] = CreateSpot("Sunbeam_02", root.transform,
            new Vector3(anchor.x + 1f, anchor.y + 8f, 0f), -70f, 10f, 42f);
        sunbeams[2] = CreateSpot("Sunbeam_03", root.transform,
            new Vector3(anchor.x + 7f, anchor.y + 7f, 0f), -50f, 8f, 30f);
        foreach (Light2D spot in sunbeams)
        {
            spot.color = living.sunlightColor;
            spot.intensity = living.sunlightIntensity;
            spot.gameObject.AddComponent<SnowglobeSunbeamSway>();
        }

        Light2D glow = CreateLight("CorruptionGlow", root.transform,
            Light2D.LightType.Point, new Vector3(anchor.x + 4f, anchor.y + 1.5f, 0f));
        glow.color = living.corruptionGlowColor;
        glow.intensity = 0f;
        glow.pointLightOuterRadius = 5f;
        glow.pointLightInnerRadius = 1f;
        glow.pointLightInnerAngle = 360f;
        glow.pointLightOuterAngle = 360f;

        Light2D mushroom = CreateLight("MushroomGlow_Independent", root.transform,
            Light2D.LightType.Point, new Vector3(anchor.x - 2f, anchor.y + 1f, 0f));
        mushroom.color = new Color32(174, 231, 170, 255);
        mushroom.intensity = 0.30f;
        mushroom.pointLightOuterRadius = 3f;
        mushroom.pointLightInnerRadius = 0.3f;
        mushroom.pointLightInnerAngle = 360f;
        mushroom.pointLightOuterAngle = 360f;

        SpriteRenderer backFog = CreateFog("Fog_Back", "FogLayer_Back.png", root.transform,
            new Vector3(anchor.x, anchor.y + 2.4f, 0f), -40, 0.12f);
        SpriteRenderer frontFog = CreateFog("Fog_Front", "FogLayer_Front.png", root.transform,
            new Vector3(anchor.x, anchor.y - 0.4f, 0f), 40, 0.08f);

        // Painted shafts are optional: the real Spot lights are already present.
        SpriteRenderer paintedRays = CreateFog("PaintedSunRays_Optional", "Sunbeam_Soft.png",
            root.transform, new Vector3(anchor.x + 1f, anchor.y + 2.5f, 0f), -15, 0.12f);
        if (paintedRays != null)
        {
            paintedRays.color = new Color(1f, 0.90f, 0.63f, 0.12f);
            paintedRays.transform.localScale = new Vector3(1.6f, 1.4f, 1f);
        }

        GameObject volumeGO = new GameObject("SnowglobeGlobalVolume");
        Undo.RegisterCreatedObjectUndo(volumeGO, "Create Post Processing Volume");
        Volume volume = volumeGO.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = postProfile;

        SnowglobeLightingController controller = root.AddComponent<SnowglobeLightingController>();
        controller.livingProfile = living;
        controller.corruptedProfile = corrupted;
        controller.ambientGlobalLight = global;
        controller.sunbeams = sunbeams;
        controller.corruptionGlows = new[] { glow };
        controller.fogLayers = new[] { backFog, frontFog };
        controller.postProcessVolume = volume;

        Camera camera = Camera.main;
        if (camera == null)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0) camera = cameras[0];
        }
        if (camera != null)
        {
            UniversalAdditionalCameraData extra = camera.GetComponent<UniversalAdditionalCameraData>();
            if (extra != null) extra.renderPostProcessing = true;
        }

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("Snowglobe Lighting created",
            "Forest lighting, fog, sunbeams and corruption profiles were created. " +
            "Position the lights, set Target Sorting Layers, enable optional volumetric lighting/shadows, " +
            "and configure post processing on Main Camera if necessary. See README.md.", "OK");
    }

    private static Light2D FindExistingGlobal()
    {
        Light2D[] all = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
        foreach (Light2D light2D in all)
            if (light2D != null && light2D.lightType == Light2D.LightType.Global)
                return light2D;
        return null;
    }

    private static Light2D CreateLight(string name, Transform parent,
        Light2D.LightType type, Vector3 position)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        Light2D result = go.AddComponent<Light2D>();
        result.lightType = type;
        return result;
    }

    private static Light2D CreateSpot(string name, Transform parent,
        Vector3 position, float zRotation, float radius, float outerAngle)
    {
        Light2D light2D = CreateLight(name, parent, Light2D.LightType.Point, position);
        light2D.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        light2D.pointLightInnerRadius = 1.3f;
        light2D.pointLightOuterRadius = radius;
        light2D.pointLightInnerAngle = outerAngle * 0.55f;
        light2D.pointLightOuterAngle = outerAngle;
        return light2D;
    }

    private static SpriteRenderer CreateFog(string name, string textureFile,
        Transform parent, Vector3 position, int sortingOrder, float alpha)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Base + "/Textures/" + textureFile);
        if (sprite == null) return null;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        SpriteRenderer renderer2D = go.AddComponent<SpriteRenderer>();
        renderer2D.sprite = sprite;
        renderer2D.sortingOrder = sortingOrder;
        renderer2D.color = new Color(1f, 1f, 1f, alpha);
        go.transform.localScale = new Vector3(4f, 2.0f, 1f);
        return renderer2D;
    }

    private static void ConfigureTexture(string file, float ppu)
    {
        string path = Base + "/Textures/" + file;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite &&
            Mathf.Approximately(importer.spritePixelsPerUnit, ppu)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = ppu;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string folder = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static SnowglobeLightingProfile GetOrCreateProfile(string name, bool isCorrupt)
    {
        string path = Base + "/Profiles/" + name;
        SnowglobeLightingProfile profile = AssetDatabase.LoadAssetAtPath<SnowglobeLightingProfile>(path);
        if (profile != null) return profile;

        profile = ScriptableObject.CreateInstance<SnowglobeLightingProfile>();
        if (isCorrupt)
        {
            profile.ambientColor = new Color32(137, 149, 177, 255);
            profile.ambientIntensity = 0.48f;
            profile.sunlightColor = new Color32(139, 188, 201, 255);
            profile.sunlightIntensity = 0.15f;
            profile.corruptionGlowColor = new Color32(73, 224, 203, 255);
            profile.corruptionGlowIntensity = 0.75f;
            profile.fogColor = new Color32(80, 112, 140, 255);
            profile.fogOpacity = 0.24f;
            profile.bloomIntensity = 0.28f;
            profile.colorFilter = new Color32(194, 217, 233, 255);
            profile.contrast = 13f;
            profile.saturation = -22f;
        }
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static VolumeProfile GetOrCreateVolume()
    {
        string path = Base + "/Profiles/ForestPostProcessing.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile != null) return profile;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.12f);
        bloom.threshold.Override(1.15f);
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(5f);
        color.saturation.Override(0f);
        color.colorFilter.Override(Color.white);
        EditorUtility.SetDirty(profile);
        return profile;
    }
}
#endif
