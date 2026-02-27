using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Sound & Haptics module.
/// Creates the SoundHaptics folder, writes SoundConfig SO, SoundManager and HapticsManager.
/// </summary>
public static class SoundHapticsInstaller
{
    private const string FolderPath          = "Assets/_Framework/SoundHaptics";
    private const string SoundManagerPath    = "Assets/_Framework/SoundHaptics/SoundManager.cs";
    private const string HapticsManagerPath  = "Assets/_Framework/SoundHaptics/HapticsManager.cs";
    private const string SoundConfigSrcPath  = "Assets/_Framework/SoundHaptics/SoundConfig.cs";
    private const string SoundConfigAsset    = "Assets/_Framework/SoundHaptics/SoundConfig.asset";

    // ─── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns true if SoundManager.cs has already been written.</summary>
    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(SoundManagerPath);

    // ─── Install ──────────────────────────────────────────────────────────────

    /// <summary>Scaffolds the Sound & Haptics module.</summary>
    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(SoundConfigSrcPath, SoundConfigTemplate());
        FrameworkModuleInstaller.WriteScript(SoundManagerPath,   SoundManagerTemplate());
        FrameworkModuleInstaller.WriteScript(HapticsManagerPath, HapticsManagerTemplate());
        EditorApplication.delayCall += CreateSoundConfigAsset;
        EditorApplication.delayCall += SetupScene;
        Debug.Log("[Framework] ✅ Sound & Haptics module installed.");
    }

    private static void SetupScene()
    {
        // HapticsManager is a static class — only SoundManager needs a GameObject
        FrameworkModuleInstaller.CreateSceneHeader("Sound & Haptics");
        FrameworkModuleInstaller.CreateSceneManager("SoundManager", "SoundManager");
    }

    private static void CreateSoundConfigAsset()
    {
        // Reflection-based: SoundConfig is written to disk by this installer;
        // avoid a compile-time reference from the Editor assembly to a runtime type.
        FrameworkModuleInstaller.CreateScriptableObject("SoundConfig", SoundConfigAsset);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string SoundConfigTemplate() => @"using System;
using UnityEngine;

/// <summary>
/// ScriptableObject that stores all named audio clips and default volume levels.
/// Assign clips in the Inspector; SoundManager resolves them by SoundType enum.
/// </summary>
[CreateAssetMenu(fileName = ""SoundConfig"", menuName = ""Framework/Sound Config"")]
public class SoundConfig : ScriptableObject
{
    [Header(""Volumes (0–1)"")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume    = 1f;
    [Range(0f, 1f)] public float musicVolume  = 0.7f;

    [Header(""Clips"")]
    public SoundEntry[] sounds;
}

/// <summary>Maps a SoundType enum value to an AudioClip.</summary>
[Serializable]
public class SoundEntry
{
    public SoundType  type;
    public AudioClip  clip;
}

/// <summary>
/// Add your game-specific sound names here.
/// SoundManager.PlaySFX(SoundType.ButtonClick) will look up this enum.
/// </summary>
public enum SoundType
{
    ButtonClick,
    ButtonBack,
    Win,
    Lose,
    Coin,
    PowerUp,
    // ─── Add more entries as needed ───────────────────────────────────────────
}
";

    private static string SoundManagerTemplate() => @"using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that manages all game audio.
/// Uses a pool of AudioSources for SFX to prevent clipping,
/// and a dedicated AudioSource for looping music tracks.
/// Reads volumes and clips from a SoundConfig ScriptableObject.
/// Mute states are persisted via SettingsManager.
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SoundManager Instance { get; private set; }

    [Header(""Config"")]
    [Tooltip(""Drag the SoundConfig.asset here"")]
    [SerializeField] private SoundConfig _config;

    [Header(""Pool size (SFX)"")]
    [SerializeField] private int _sfxPoolSize = 8;

    // ─── Internal ─────────────────────────────────────────────────────────────
    private AudioSource              _musicSource;
    private List<AudioSource>        _sfxPool     = new();
    private Dictionary<SoundType, AudioClip> _clipMap = new();
    private int                      _poolIndex;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildClipMap();
        BuildSFXPool();
        SetupMusicSource();
    }

    // ─── Initialisation helpers ───────────────────────────────────────────────

    private void BuildClipMap()
    {
        if (_config == null || _config.sounds == null) return;
        foreach (var entry in _config.sounds)
            _clipMap[entry.type] = entry.clip;
    }

    private void BuildSFXPool()
    {
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _sfxPool.Add(source);
        }
    }

    private void SetupMusicSource()
    {
        _musicSource           = gameObject.AddComponent<AudioSource>();
        _musicSource.loop      = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume    = _config != null ? _config.musicVolume * _config.masterVolume : 1f;
    }

    // ─── Public SFX API ───────────────────────────────────────────────────────

    /// <summary>Plays a one-shot SFX by SoundType enum value.</summary>
    public void PlaySFX(SoundType type)
    {
        if (!_clipMap.TryGetValue(type, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($""[Sound] No clip mapped for {type}"");
            return;
        }
        var source = GetNextPooledSource();
        source.volume = (_config != null ? _config.sfxVolume * _config.masterVolume : 1f);
        source.PlayOneShot(clip);
    }

    /// <summary>Plays an AudioClip directly (useful for dynamic clips).</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        var source = GetNextPooledSource();
        source.volume = (_config != null ? _config.sfxVolume * _config.masterVolume : 1f) * volumeScale;
        source.PlayOneShot(clip);
    }

    // ─── Public Music API ─────────────────────────────────────────────────────

    /// <summary>Starts playing a music track; optionally loops it.</summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        _musicSource.clip   = clip;
        _musicSource.loop   = loop;
        _musicSource.volume = _config != null ? _config.musicVolume * _config.masterVolume : 0.7f;
        _musicSource.Play();
    }

    /// <summary>Stops the currently playing music track.</summary>
    public void StopMusic() => _musicSource.Stop();

    /// <summary>Pauses the music without resetting position.</summary>
    public void PauseMusic() => _musicSource.Pause();

    /// <summary>Resumes paused music.</summary>
    public void ResumeMusic() => _musicSource.UnPause();

    // ─── Volume control ───────────────────────────────────────────────────────

    /// <summary>Sets the master volume (0–1) and updates all active sources.</summary>
    public void SetMasterVolume(float volume)
    {
        if (_config == null) return;
        _config.masterVolume      = Mathf.Clamp01(volume);
        _musicSource.volume       = _config.musicVolume * _config.masterVolume;
    }

    /// <summary>Mutes or unmutes all audio.</summary>
    public void SetMute(bool mute)
    {
        AudioListener.volume = mute ? 0f : 1f;
    }

    // ─── Pool helper ──────────────────────────────────────────────────────────

    private AudioSource GetNextPooledSource()
    {
        var source = _sfxPool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sfxPool.Count;
        return source;
    }
}
";

    private static string HapticsManagerTemplate() => @"using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Cross-platform haptic feedback manager.
/// Android  → Handheld.Vibrate() and VibrationEffect via JNI.
/// iOS      → UIImpactFeedbackGenerator via DllImport Taptic engine.
/// All calls are platform-guarded; safe to call on any platform.
/// </summary>
public static class HapticsManager
{
#if UNITY_IOS
    // iOS native Taptic engine bridge (requires CoreHaptics / UIKit)
    [DllImport(""__Internal"")]
    private static extern void _TapticLight();
    [DllImport(""__Internal"")]
    private static extern void _TapticMedium();
    [DllImport(""__Internal"")]
    private static extern void _TapticHeavy();
#endif

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Triggers a short, light haptic tap (best for UI interactions).</summary>
    public static void LightImpact()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrationAndroid(30);
#elif UNITY_IOS && !UNITY_EDITOR
        _TapticLight();
#endif
    }

    /// <summary>Triggers a medium strength haptic tap.</summary>
    public static void MediumImpact()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrationAndroid(60);
#elif UNITY_IOS && !UNITY_EDITOR
        _TapticMedium();
#endif
    }

    /// <summary>Triggers a strong haptic bump (best for significant events like win/lose).</summary>
    public static void HeavyImpact()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrationAndroid(100);
#elif UNITY_IOS && !UNITY_EDITOR
        _TapticHeavy();
#endif
    }

    /// <summary>
    /// Vibrates the device for a precise duration in milliseconds (Android only).
    /// Use LightImpact / MediumImpact on iOS for the closest equivalent.
    /// </summary>
    public static void Vibrate(long milliseconds = 50)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrationAndroid(milliseconds);
#endif
    }

    // ─── Android JNI helper ───────────────────────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Triggers a timed vibration on Android using VibrationEffect (API 26+).</summary>
    private static void VibrationAndroid(long ms)
    {
        try
        {
            using var unityPlayer   = new AndroidJavaClass(""com.unity3d.player.UnityPlayer"");
            using var activity      = unityPlayer.GetStatic<AndroidJavaObject>(""currentActivity"");
            using var vibratorService = activity.Call<AndroidJavaObject>(""getSystemService"", ""vibrator"");

            if (vibratorService == null) return;

            // Use VibrationEffect on API 26+
            if (AndroidVersion() >= 26)
            {
                using var vibEffect = new AndroidJavaClass(""android.os.VibrationEffect"");
                using var effect    = vibEffect.CallStatic<AndroidJavaObject>(
                    ""createOneShot"", ms, -1); // -1 = default amplitude
                vibratorService.Call(""vibrate"", effect);
            }
            else
            {
                // Fallback for older devices
#pragma warning disable CS0618
                vibratorService.Call(""vibrate"", ms);
#pragma warning restore CS0618
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($""[Haptics] Android vibration failed: {ex.Message}"");
        }
    }

    private static int AndroidVersion()
    {
        using var version = new AndroidJavaClass(""android.os.Build$VERSION"");
        return version.GetStatic<int>(""SDK_INT"");
    }
#endif
}
";
}
