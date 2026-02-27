using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Ads module.
/// Creates the Ads folder, writes AdsConfig.cs + AdsManager.cs and creates AdsConfig.asset.
/// Supports AppLovin MAX (MAX_SDK) and Google AdMob (ADMOB_SDK) via compile-time defines.
/// </summary>
public static class AdsInstaller
{
    private const string FolderPath = "Assets/_Framework/Ads";
    private const string ScriptPath = "Assets/_Framework/Ads/AdsManager.cs";
    private const string ConfigPath = "Assets/_Framework/Ads/AdsConfig.cs";
    private const string AssetPath  = "Assets/_Framework/Ads/AdsConfig.asset";

    // ─── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns true if the Ads module is already installed.</summary>
    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(ScriptPath);

    // ─── Install ──────────────────────────────────────────────────────────────

    /// <summary>Scaffolds the Ads module — folder, scripts and SO asset.</summary>
    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(ConfigPath, AdsConfigTemplate());
        FrameworkModuleInstaller.WriteScript(ScriptPath, AdsManagerTemplate());
        // SO and scene setup deferred until after Unity compiles the new types
        EditorApplication.delayCall += CreateAdsConfigAsset;
        EditorApplication.delayCall += SetupScene;
        Debug.Log("[Framework] ✅ Ads module installed.");
    }

    private static void SetupScene()
    {
        FrameworkModuleInstaller.CreateSceneHeader("Ads");
        FrameworkModuleInstaller.CreateSceneManager("AdsManager", "AdsManager");
    }

    private static void CreateAdsConfigAsset()
    {
        // Reflection-based so the Editor assembly has no compile-time reference to AdsConfig.
        FrameworkModuleInstaller.CreateScriptableObject("AdsConfig", AssetPath);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string AdsConfigTemplate() => @"using UnityEngine;

/// <summary>Selects which set of ad unit IDs AdsManager uses at runtime.</summary>
public enum AdsEnvironment { Dev, Prod }

/// <summary>
/// ScriptableObject that stores ad unit IDs for both Dev and Prod environments,
/// separated by platform (Android / iOS) and ad type (Banner / Interstitial / Rewarded).
/// Dev slots are pre-filled with Google's official AdMob test IDs so ads render
/// immediately without a live AdMob account.
/// Switch environment: Tools ▶ Build ▶ Switch To Dev  /  Switch To Prod
/// </summary>
[CreateAssetMenu(fileName = ""AdsConfig"", menuName = ""Framework/Ads Config"")]
public class AdsConfig : ScriptableObject
{
    // ─── Global ───────────────────────────────────────────────────────────────
    [Header(""Global"")]
    public AdsEnvironment activeEnvironment = AdsEnvironment.Dev;
    public bool isAdsEnabled         = true;
    public bool isRemoveAdsPurchased = false;

    // ─── Dev — Android (Google AdMob test IDs) ────────────────────────────────
    [Header(""Dev — Android (AdMob Test IDs)"")]
    public string dev_banner_android       = ""ca-app-pub-3940256099942544/6300978111"";
    public string dev_interstitial_android = ""ca-app-pub-3940256099942544/1033173712"";
    public string dev_rewarded_android     = ""ca-app-pub-3940256099942544/5224354917"";

    // ─── Dev — iOS (Google AdMob test IDs) ───────────────────────────────────
    [Header(""Dev — iOS (AdMob Test IDs)"")]
    public string dev_banner_ios           = ""ca-app-pub-3940256099942544/2934735716"";
    public string dev_interstitial_ios     = ""ca-app-pub-3940256099942544/4411468910"";
    public string dev_rewarded_ios         = ""ca-app-pub-3940256099942544/1712485313"";

    // ─── Prod — Android (fill in your live unit IDs) ─────────────────────────
    [Header(""Prod — Android"")]
    public string prod_banner_android       = """";
    public string prod_interstitial_android = """";
    public string prod_rewarded_android     = """";

    // ─── Prod — iOS (fill in your live unit IDs) ──────────────────────────────
    [Header(""Prod — iOS"")]
    public string prod_banner_ios           = """";
    public string prod_interstitial_ios     = """";
    public string prod_rewarded_ios         = """";

    // ─── Resolved getters (called by AdsManager) ─────────────────────────────

    /// <summary>Banner ID for the active environment and current platform.</summary>
    public string BannerId =>
#if UNITY_IOS
        activeEnvironment == AdsEnvironment.Dev ? dev_banner_ios       : prod_banner_ios;
#else
        activeEnvironment == AdsEnvironment.Dev ? dev_banner_android   : prod_banner_android;
#endif

    /// <summary>Interstitial ID for the active environment and current platform.</summary>
    public string InterstitialId =>
#if UNITY_IOS
        activeEnvironment == AdsEnvironment.Dev ? dev_interstitial_ios       : prod_interstitial_ios;
#else
        activeEnvironment == AdsEnvironment.Dev ? dev_interstitial_android   : prod_interstitial_android;
#endif

    /// <summary>Rewarded ID for the active environment and current platform.</summary>
    public string RewardedId =>
#if UNITY_IOS
        activeEnvironment == AdsEnvironment.Dev ? dev_rewarded_ios       : prod_rewarded_ios;
#else
        activeEnvironment == AdsEnvironment.Dev ? dev_rewarded_android   : prod_rewarded_android;
#endif
}
";

    private static string AdsManagerTemplate() => @"using System;
using UnityEngine;

#if MAX_SDK
// AppLovin MAX SDK — import via Unity Package Manager or .unitypackage
#endif
#if ADMOB_SDK
using GoogleMobileAds.Api;
#endif

/// <summary>
/// Singleton that abstracts Banner, Interstitial and Rewarded ads.
/// All ad unit IDs are resolved automatically by AdsConfig based on the
/// active BuildEnvironment (Dev uses Google test IDs, Prod uses live IDs).
/// Add MAX_SDK or ADMOB_SDK to Scripting Define Symbols to activate the chosen network.
/// </summary>
public class AdsManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static AdsManager Instance { get; private set; }

    [Header(""Config"")]
    [Tooltip(""Drag the AdsConfig.asset here"")]
    [SerializeField] private AdsConfig _config;

    // ─── Callbacks ────────────────────────────────────────────────────────────
    private Action       _onInterstitialComplete;
    private Action<bool> _onRewardedComplete;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitAds();
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

    private void InitAds()
    {
        if (_config == null)
        {
            Debug.LogError(""[Ads] AdsConfig not assigned on AdsManager!"");
            return;
        }

        Debug.Log($""[Ads] Initialising in {_config.activeEnvironment} mode."");

#if MAX_SDK
        MaxSdkCallbacks.OnSdkInitializedEvent += _ => LoadAllAds();
        MaxSdk.SetSdkKey(""YOUR_MAX_SDK_KEY"");
        MaxSdk.InitializeSdk();
#elif ADMOB_SDK
        MobileAds.Initialize(_ => LoadAllAds());
#else
        Debug.LogWarning(""[Ads] No SDK define. Add MAX_SDK or ADMOB_SDK to Scripting Define Symbols."");
#endif
    }

    private void LoadAllAds() { LoadInterstitial(); LoadRewarded(); }

    // ─── Banner ───────────────────────────────────────────────────────────────

    /// <summary>Shows a banner at the bottom of the screen.</summary>
    public void ShowBanner()
    {
        if (!AdsAllowed()) return;
#if MAX_SDK
        MaxSdk.CreateBanner(_config.BannerId, MaxSdkBase.BannerPosition.BottomCenter);
        MaxSdk.ShowBanner(_config.BannerId);
#elif ADMOB_SDK
        // TODO: create BannerView(_config.BannerId, AdSize.Banner, AdPosition.Bottom)
#endif
    }

    /// <summary>Hides and destroys the active banner.</summary>
    public void HideBanner()
    {
#if MAX_SDK
        MaxSdk.HideBanner(_config.BannerId);
#endif
    }

    // ─── Interstitial ─────────────────────────────────────────────────────────

    private void LoadInterstitial()
    {
#if MAX_SDK
        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (id, _) =>
        {
            LoadInterstitial();
            _onInterstitialComplete?.Invoke();
        };
        MaxSdk.LoadInterstitial(_config.InterstitialId);
#endif
    }

    /// <summary>Shows an interstitial; calls <paramref name=""onComplete""/> when it closes.</summary>
    public void ShowInterstitial(Action onComplete = null)
    {
        if (!AdsAllowed()) { onComplete?.Invoke(); return; }
        _onInterstitialComplete = onComplete;
#if MAX_SDK
        if (MaxSdk.IsInterstitialReady(_config.InterstitialId))
            MaxSdk.ShowInterstitial(_config.InterstitialId);
        else
            onComplete?.Invoke();
#else
        onComplete?.Invoke();
#endif
    }

    // ─── Rewarded ─────────────────────────────────────────────────────────────

    private void LoadRewarded()
    {
#if MAX_SDK
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (id, _, _) => _onRewardedComplete?.Invoke(true);
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         += (id, _)    => LoadRewarded();
        MaxSdk.LoadRewardedAd(_config.RewardedId);
#endif
    }

    /// <summary>
    /// Shows a rewarded ad. Calls <paramref name=""onReward""/> with true if reward earned,
    /// false if skipped or unavailable.
    /// </summary>
    public void ShowRewarded(Action<bool> onReward = null)
    {
        _onRewardedComplete = onReward;
#if MAX_SDK
        if (MaxSdk.IsRewardedAdReady(_config.RewardedId))
            MaxSdk.ShowRewardedAd(_config.RewardedId);
        else
            onReward?.Invoke(false);
#else
        onReward?.Invoke(false);
#endif
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns false when ads are globally disabled or Remove Ads is purchased.</summary>
    private bool AdsAllowed() => _config != null && _config.isAdsEnabled && !_config.isRemoveAdsPurchased;
}
";
}
