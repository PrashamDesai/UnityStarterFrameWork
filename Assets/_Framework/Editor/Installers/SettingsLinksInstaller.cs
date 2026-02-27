using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Settings & Links module.
/// Creates SettingsManager.cs, LinksManager.cs and the GameLinks ScriptableObject.
/// </summary>
public static class SettingsLinksInstaller
{
    private const string FolderPath          = "Assets/_Framework/Settings";
    private const string SettingsManagerPath = "Assets/_Framework/Settings/SettingsManager.cs";
    private const string LinksManagerPath    = "Assets/_Framework/Settings/LinksManager.cs";
    private const string GameLinksSrcPath    = "Assets/_Framework/Settings/GameLinks.cs";
    private const string GameLinksAsset      = "Assets/_Framework/Settings/GameLinks.asset";

    // ─── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns true if SettingsManager.cs is already installed.</summary>
    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(SettingsManagerPath);

    // ─── Install ──────────────────────────────────────────────────────────────

    /// <summary>Scaffolds the Settings & Links module.</summary>
    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(GameLinksSrcPath,    GameLinksTemplate());
        FrameworkModuleInstaller.WriteScript(SettingsManagerPath, SettingsManagerTemplate());
        FrameworkModuleInstaller.WriteScript(LinksManagerPath,    LinksManagerTemplate());
        EditorApplication.delayCall += CreateGameLinksAsset;
        EditorApplication.delayCall += SetupScene;
        Debug.Log("[Framework] ✅ Settings & Links module installed.");
    }

    private static void SetupScene()
    {
        FrameworkModuleInstaller.CreateSceneHeader("Settings & Links");
        FrameworkModuleInstaller.CreateSceneManager("SettingsManager", "SettingsManager");
        FrameworkModuleInstaller.CreateSceneManager("LinksManager",    "LinksManager");
    }

    private static void CreateGameLinksAsset()
    {
        // Reflection-based: GameLinks is a runtime type written by this installer;
        // avoid a compile-time dependency from the Editor assembly.
        FrameworkModuleInstaller.CreateScriptableObject("GameLinks", GameLinksAsset);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string GameLinksTemplate() => @"using UnityEngine;

/// <summary>
/// ScriptableObject that stores all external URLs for the game.
/// Drag GameLinks.asset onto LinksManager in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = ""GameLinks"", menuName = ""Framework/Game Links"")]
public class GameLinks : ScriptableObject
{
    [Header(""Rate Us"")]
    [Tooltip(""Google Play store URL for rating"")]
    public string rateUsAndroid = ""https://play.google.com/store/apps/details?id=com.company.mygame"";
    [Tooltip(""App Store URL for rating"")]
    public string rateUsIOS     = ""https://apps.apple.com/app/idXXXXXXXXXX"";

    [Header(""Feedback"")]
    [Tooltip(""Google Form or Typeform URL for player feedback"")]
    public string feedbackFormUrl = ""https://forms.gle/XXXXXXXXXX"";

    [Header(""Deep Link"")]
    [Tooltip(""Universal / App Link used to deep-link into the game from external sources"")]
    public string deepLinkUrl = ""mygame://open"";

    [Header(""More Games"")]
    [Tooltip(""Google Play developer page to show more games"")]
    public string moreGamesAndroid = ""https://play.google.com/store/apps/developer?id=YourCompany"";
    [Tooltip(""App Store developer page to show more games"")]
    public string moreGamesIOS     = ""https://apps.apple.com/developer/yourcompany/idXXXXXXXXXX"";
}
";

    private static string SettingsManagerTemplate() => @"using System;
using UnityEngine;

/// <summary>
/// Singleton that manages persistent game settings backed by PlayerPrefs.
/// Settings: Sound, Haptics, Push Notifications.
/// Events allow UI elements to react immediately to changes without polling.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static SettingsManager Instance { get; private set; }

    // ─── PlayerPrefs keys ─────────────────────────────────────────────────────
    private const string KEY_SOUND        = ""setting_sound"";
    private const string KEY_HAPTICS      = ""setting_haptics"";
    private const string KEY_NOTIFICATIONS = ""setting_notifications"";

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired whenever the sound setting changes. Arg: new muted state.</summary>
    public static event Action<bool> OnSoundToggled;
    /// <summary>Fired whenever the haptics setting changes. Arg: new enabled state.</summary>
    public static event Action<bool> OnHapticsToggled;
    /// <summary>Fired whenever the notifications setting changes. Arg: new enabled state.</summary>
    public static event Action<bool> OnNotificationsToggled;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Sound ────────────────────────────────────────────────────────────────

    /// <summary>Returns true if sound is NOT muted (default: on).</summary>
    public bool IsSoundOn => PlayerPrefs.GetInt(KEY_SOUND, 1) == 1;

    /// <summary>Toggles sound and fires OnSoundToggled.</summary>
    public void ToggleSound()
    {
        bool next = !IsSoundOn;
        PlayerPrefs.SetInt(KEY_SOUND, next ? 1 : 0);
        PlayerPrefs.Save();
        AudioListener.volume = next ? 1f : 0f;
        OnSoundToggled?.Invoke(next);
    }

    /// <summary>Sets sound on or off explicitly.</summary>
    public void SetSound(bool on)
    {
        PlayerPrefs.SetInt(KEY_SOUND, on ? 1 : 0);
        PlayerPrefs.Save();
        AudioListener.volume = on ? 1f : 0f;
        OnSoundToggled?.Invoke(on);
    }

    // ─── Haptics ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if haptics are enabled (default: on).</summary>
    public bool IsHapticsOn => PlayerPrefs.GetInt(KEY_HAPTICS, 1) == 1;

    /// <summary>Toggles haptics and fires OnHapticsToggled.</summary>
    public void ToggleHaptics()
    {
        bool next = !IsHapticsOn;
        PlayerPrefs.SetInt(KEY_HAPTICS, next ? 1 : 0);
        PlayerPrefs.Save();
        OnHapticsToggled?.Invoke(next);
    }

    /// <summary>Sets haptics on or off explicitly.</summary>
    public void SetHaptics(bool on)
    {
        PlayerPrefs.SetInt(KEY_HAPTICS, on ? 1 : 0);
        PlayerPrefs.Save();
        OnHapticsToggled?.Invoke(on);
    }

    // ─── Notifications ────────────────────────────────────────────────────────

    /// <summary>Returns true if push notifications are enabled (default: on).</summary>
    public bool IsNotificationsOn => PlayerPrefs.GetInt(KEY_NOTIFICATIONS, 1) == 1;

    /// <summary>Toggles notifications and fires OnNotificationsToggled.</summary>
    public void ToggleNotifications()
    {
        bool next = !IsNotificationsOn;
        PlayerPrefs.SetInt(KEY_NOTIFICATIONS, next ? 1 : 0);
        PlayerPrefs.Save();
        OnNotificationsToggled?.Invoke(next);
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    /// <summary>Resets all settings to factory defaults.</summary>
    public void ResetToDefaults()
    {
        SetSound(true);
        SetHaptics(true);
        PlayerPrefs.SetInt(KEY_NOTIFICATIONS, 1);
        PlayerPrefs.Save();
        Debug.Log(""[Settings] All settings reset to defaults."");
    }
}
";

    private static string LinksManagerTemplate() => @"using UnityEngine;

/// <summary>
/// Singleton that opens external URLs from the GameLinks ScriptableObject.
/// Attach to a persistent Manager GameObject and assign the GameLinks.asset.
/// </summary>
public class LinksManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static LinksManager Instance { get; private set; }

    [Header(""Links Config"")]
    [Tooltip(""Drag the GameLinks.asset here"")]
    [SerializeField] private GameLinks _links;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Opens the platform-appropriate store page to rate the game.</summary>
    public void OpenRateUs()
    {
        if (_links == null) return;
#if UNITY_ANDROID
        OpenUrl(_links.rateUsAndroid);
#else
        OpenUrl(_links.rateUsIOS);
#endif
    }

    /// <summary>Opens the feedback form URL (Google Form, Typeform, etc.).</summary>
    public void OpenFeedbackForm()
    {
        if (_links == null) return;
        OpenUrl(_links.feedbackFormUrl);
    }

    /// <summary>
    /// Navigates using the deep link URL (e.g. mygame://open).
    /// Handles both URI schemes and universal/App Links.
    /// </summary>
    public void OpenDeepLink()
    {
        if (_links == null) return;
        OpenUrl(_links.deepLinkUrl);
    }

    /// <summary>Opens the platform-appropriate developer page to show more games.</summary>
    public void OpenMoreGames()
    {
        if (_links == null) return;
#if UNITY_ANDROID
        OpenUrl(_links.moreGamesAndroid);
#else
        OpenUrl(_links.moreGamesIOS);
#endif
    }

    /// <summary>Opens any URL in the device's default browser or handler.</summary>
    public void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning(""[Links] Attempted to open an empty URL."");
            return;
        }
        Application.OpenURL(url);
    }
}
";
}
