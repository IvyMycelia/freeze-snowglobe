using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Music + ambience crossfades for overlapping areas. One instance per scene.
/// No reliance on background loading or scene reloading.
/// </summary>
public sealed class SnowglobeZoneAudioManager : MonoBehaviour
{
    public static SnowglobeZoneAudioManager Instance { get; private set; }

    [Header("Fallback audio (outside any zone)")]
    [SerializeField] private AudioClip defaultMusic;
    [SerializeField] private AudioClip defaultAmbience;

    [Header("Mixing")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.45f;
    [SerializeField, Min(0f)] private float defaultCrossfadeSeconds = 2.5f;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup ambienceMixerGroup;

    [Header("Temporary danger music (optional)")]
    [SerializeField] private AudioClip combatMusic;
    [SerializeField, Min(0.1f)] private float combatHoldSeconds = 7f;

    private readonly List<SnowglobeAudioZone> activeZones = new List<SnowglobeAudioZone>();
    private AudioPair music;
    private AudioPair ambience;
    private float combatEndsAt = -1f;
    private AudioClip cinematicMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        music = new AudioPair(transform, "Music", musicMixerGroup, musicVolume);
        ambience = new AudioPair(transform, "Ambience", ambienceMixerGroup, ambienceVolume);
    }

    private void Start()
    {
        RefreshAudio();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        music.Tick(dt);
        ambience.Tick(dt);

        if (combatEndsAt >= 0f && Time.unscaledTime >= combatEndsAt)
        {
            combatEndsAt = -1f;
            RefreshAudio();
        }
    }

    public void EnterZone(SnowglobeAudioZone zone)
    {
        if (zone == null) return;
        activeZones.Remove(zone); // Equal-priority zones: latest entered wins.
        activeZones.Add(zone);
        RefreshAudio();
    }

    public void ExitZone(SnowglobeAudioZone zone)
    {
        if (zone == null) return;
        if (activeZones.Remove(zone)) RefreshAudio();
    }

    /// <summary>Hook this into FloraEnemy's On Alarm UnityEvent.</summary>
    public void TriggerCombatMusic()
    {
        if (combatMusic == null) return;
        combatEndsAt = Time.unscaledTime + combatHoldSeconds;
        RefreshAudio();
    }

    /// <summary>Optional cutscene override. Pass null or call ClearCutsceneMusic to restore area music.</summary>
    public void SetCutsceneMusic(AudioClip clip)
    {
        cinematicMusic = clip;
        RefreshAudio();
    }

    public void ClearCutsceneMusic()
    {
        cinematicMusic = null;
        RefreshAudio();
    }

    private void RefreshAudio()
    {
        SnowglobeAudioZone best = null;
        foreach (SnowglobeAudioZone zone in activeZones)
        {
            if (zone == null || !zone.isActiveAndEnabled) continue;
            if (best == null || zone.Priority >= best.Priority)
                best = zone;
        }

        float fade = best != null && best.FadeSeconds >= 0f
            ? best.FadeSeconds
            : defaultCrossfadeSeconds;

        AudioClip zoneMusic = best != null && best.Music != null
            ? best.Music : defaultMusic;
        AudioClip zoneAmbience = best != null && best.Ambience != null
            ? best.Ambience : defaultAmbience;

        AudioClip requestedMusic = cinematicMusic != null
            ? cinematicMusic
            : (combatEndsAt >= 0f && combatMusic != null ? combatMusic : zoneMusic);

        music.TransitionTo(requestedMusic, fade);
        ambience.TransitionTo(zoneAmbience, fade);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Two AudioSources are used to crossfade without abruptly cutting either clip.</summary>
    private sealed class AudioPair
    {
        private readonly AudioSource a;
        private readonly AudioSource b;
        private readonly float maxVolume;
        private AudioClip requestedClip;
        private float startA, startB;
        private float endA, endB;
        private float elapsed, fadeDuration;

        public AudioPair(Transform parent, string name, AudioMixerGroup mixer, float volume)
        {
            a = MakeSource(parent, name + "_A", mixer);
            b = MakeSource(parent, name + "_B", mixer);
            maxVolume = volume;
        }

        private static AudioSource MakeSource(Transform parent, string name, AudioMixerGroup mixer)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D: same volume across the whole map.
            source.volume = 0f;
            source.outputAudioMixerGroup = mixer;
            return source;
        }

        public void TransitionTo(AudioClip clip, float seconds)
        {
            if (clip == requestedClip) return;
            requestedClip = clip;

            AudioSource selected = null;
            if (clip != null)
            {
                if (a.isPlaying && a.clip == clip) selected = a;
                else if (b.isPlaying && b.clip == clip) selected = b;
                else
                {
                    // Replace the quieter source; keep the louder one audible during fade.
                    selected = a.volume <= b.volume ? a : b;
                    selected.Stop();
                    selected.clip = clip;
                    selected.volume = 0f;
                    selected.Play();
                }
            }

            startA = a.volume;
            startB = b.volume;
            endA = selected == a ? maxVolume : 0f;
            endB = selected == b ? maxVolume : 0f;
            fadeDuration = Mathf.Max(0f, seconds);
            elapsed = 0f;
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            float t = fadeDuration < 0.0001f
                ? 1f : Mathf.Clamp01(elapsed / fadeDuration);
            float smooth = t * t * (3f - 2f * t);
            a.volume = Mathf.Lerp(startA, endA, smooth);
            b.volume = Mathf.Lerp(startB, endB, smooth);

            if (t >= 1f)
            {
                if (endA == 0f && a.isPlaying) a.Stop();
                if (endB == 0f && b.isPlaying) b.Stop();
            }
        }
    }
}
