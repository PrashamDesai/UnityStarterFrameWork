using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Build Scripts module.
/// BuildConfig stores separate App Identity for Dev and Prod.
/// BuildScript applies the identity for the active environment when building.
/// </summary>
public static class BuildInstaller
{
    private const string FolderPath       = "Assets/_Framework/Editor/Build";
    private const string BuildScriptPath  = "Assets/_Framework/Editor/Build/BuildScript.cs";
    private const string ConfigScriptPath = "Assets/_Framework/Editor/Build/BuildConfig.cs";
    private const string AssetPath        = "Assets/_Framework/Editor/Build/BuildConfig.asset";

    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(BuildScriptPath);

    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(ConfigScriptPath, BuildConfigTemplate());
        FrameworkModuleInstaller.WriteScript(BuildScriptPath,  BuildScriptTemplate());
        EditorApplication.delayCall += CreateBuildConfigAsset;
        Debug.Log("[Framework] ✅ Build Scripts module installed.");
    }

    private static void CreateBuildConfigAsset()
    {
        FrameworkModuleInstaller.CreateScriptableObject("BuildConfig", AssetPath);
    }

    // ─── Templates ────────────────────────────────────────────────────────────

    private static string BuildConfigTemplate() => @"using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selects which environment's identity and ad IDs to use.
/// Dev  → test bundle ID, test ad IDs.
/// Prod → live bundle ID, live ad IDs.
/// Switch via Tools ▶ Build ▶ Switch To Dev / Switch To Prod.
/// </summary>
public enum BuildEnvironment { Dev, Prod }

/// <summary>
/// Per-environment app identity block.
/// Stores everything that differs between a dev sideload and a store release.
/// </summary>
[Serializable]
public class AppIdentity
{
    [Tooltip(""Product name shown on the device home screen"")]
    public string appName     = ""MyGame"";

    [Tooltip(""Unique reverse-domain bundle / application ID"")]
    public string bundleId    = ""com.company.mygame"";

    [Tooltip(""Human-readable semantic version shown in stores (e.g. 1.0.0)"")]
    public string versionName = ""1.0.0"";

    [Tooltip(""Integer build number. Auto-incremented before each build."")]
    public int    versionCode = 1;
}

/// <summary>
/// ScriptableObject centralising all build settings for the project.
/// Fill in both Dev and Prod sections, then switch environments from
/// Tools ▶ Build before creating any build.
/// </summary>
[CreateAssetMenu(fileName = ""BuildConfig"", menuName = ""Framework/Build Config"")]
public class BuildConfig : ScriptableObject
{
    // ─── Environment ──────────────────────────────────────────────────────────
    [Header(""Active Environment"")]
    [Tooltip(""Which identity block and ad IDs will be used for the next build"")]
    public BuildEnvironment activeEnvironment = BuildEnvironment.Dev;

    // ─── App Identity (per environment) ──────────────────────────────────────
    [Header(""Dev — App Identity"")]
    public AppIdentity dev = new AppIdentity
    {
        appName     = ""MyGame (Dev)"",
        bundleId    = ""com.company.mygame.dev"",
        versionName = ""0.1.0"",
        versionCode = 1
    };

    [Header(""Prod — App Identity"")]
    public AppIdentity prod = new AppIdentity
    {
        appName     = ""MyGame"",
        bundleId    = ""com.company.mygame"",
        versionName = ""1.0.0"",
        versionCode = 1
    };

    // ─── Android Keystore (shared, or split if needed) ───────────────────────
    [Header(""Android Keystore"")]
    public string keystorePath = """";
    public string keystorePass = """";
    public string keyAlias     = """";
    public string keyPass      = """";

    // ─── Scenes (in build order) ─────────────────────────────────────────────
    [Header(""Scenes"")]
    public List<string> scenes = new List<string> { ""Assets/Scenes/SampleScene.unity"" };

    // ─── Output paths ─────────────────────────────────────────────────────────
    [Header(""Output Paths"")]
    public string androidOutputPath = ""Builds/Android/game.apk"";
    public string iosOutputPath     = ""Builds/iOS"";

    // ─── Resolved identity ────────────────────────────────────────────────────
    /// <summary>Returns the identity block for the active environment.</summary>
    public AppIdentity ActiveIdentity =>
        activeEnvironment == BuildEnvironment.Dev ? dev : prod;
}
";

    private static string BuildScriptTemplate() => @"using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click build pipeline. Reads all settings from BuildConfig SO.
/// Uses ActiveIdentity to apply the correct app name / bundle ID / version per environment.
/// Menu: Tools ▶ Build ▶ Android | iOS | Switch To Dev | Switch To Prod | Set Build Settings
/// </summary>
public static class BuildScript
{
    private const string BuildConfigPath = ""Assets/_Framework/Editor/Build/BuildConfig.asset"";
    private const string AdsConfigPath   = ""Assets/_Framework/Ads/AdsConfig.asset"";

    // ─── Build ────────────────────────────────────────────────────────────────

    [MenuItem(""Tools/Build/Android"")]
    public static void BuildAndroid() => Build(BuildTarget.Android);

    [MenuItem(""Tools/Build/iOS"")]
    public static void BuildIOS() => Build(BuildTarget.iOS);

    // ─── Environment switch ───────────────────────────────────────────────────

    /// <summary>Switch to Dev — applies dev identity + AdMob test IDs.</summary>
    [MenuItem(""Tools/Build/Switch To Dev"")]
    public static void SwitchToDev() => SetEnvironment(BuildEnvironment.Dev);

    /// <summary>Switch to Prod — applies live identity + live ad unit IDs.</summary>
    [MenuItem(""Tools/Build/Switch To Prod"")]
    public static void SwitchToProd() => SetEnvironment(BuildEnvironment.Prod);

    /// <summary>
    /// Applies PlayerSettings for the active environment without triggering a build.
    /// Useful to verify settings before a CI hand-off.
    /// </summary>
    [MenuItem(""Tools/Build/Set Build Settings"")]
    public static void SetBuildSettings()
    {
        var config = LoadBuildConfig();
        if (config == null) return;
        ApplyPlayerSettings(config, EditorUserBuildSettings.activeBuildTarget, bump: false);
        Debug.Log($""[Build] ✅ Settings applied — {EditorUserBuildSettings.activeBuildTarget}, "" +
                  $""{config.activeEnvironment} env, bundleId: {config.ActiveIdentity.bundleId}."");
    }

    // ─── Core build ───────────────────────────────────────────────────────────

    private static void Build(BuildTarget target)
    {
        var config = LoadBuildConfig();
        if (config == null) return;

        ApplyPlayerSettings(config, target, bump: true);

        string path = target == BuildTarget.Android
            ? config.androidOutputPath
            : config.iosOutputPath;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = config.scenes.ToArray(),
            locationPathName = path,
            target           = target,
            options          = BuildOptions.None
        });

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($""[Build] ✅ {target} succeeded → {path} ({report.summary.totalSize / 1024 / 1024} MB)"");
        else
            Debug.LogError($""[Build] ❌ {target} failed — {report.summary.totalErrors} error(s)."");
    }

    // ─── Environment switcher ─────────────────────────────────────────────────

    private static void SetEnvironment(BuildEnvironment env)
    {
        bool changed = false;

        // ── BuildConfig ──────────────────────────────────────────────────────
        var buildCfg = LoadBuildConfig();
        if (buildCfg != null && buildCfg.activeEnvironment != env)
        {
            buildCfg.activeEnvironment = env;
            EditorUtility.SetDirty(buildCfg);
            changed = true;
        }

        // ── AdsConfig (optional — only if Ads module is installed) ───────────
        // AdsConfig.activeEnvironment is AdsEnvironment {Dev=0, Prod=1}.
        // We match by ordinal — both enums share the same Dev=0, Prod=1 layout.
        var adsCfgObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AdsConfigPath);
        if (adsCfgObj != null)
        {
            var so      = new SerializedObject(adsCfgObj);
            var envProp = so.FindProperty(""activeEnvironment"");
            if (envProp != null)
            {
                int target = (int)env; // Dev=0, Prod=1
                if (envProp.intValue != target)
                {
                    envProp.intValue = target;
                    so.ApplyModifiedProperties();
                    changed = true;
                }
            }
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            var id = buildCfg != null ? buildCfg.ActiveIdentity : null;
            Debug.Log($""[Build] ✅ Switched to {env}."" +
                      (id != null ? $"" Bundle: {id.bundleId}, App: {id.appName}"" : """") +
                      (env == BuildEnvironment.Dev ? "" | AdMob test IDs active."" : "" | Live ad IDs active.""));
        }
        else
        {
            Debug.Log($""[Build] Already in {env} environment."");
        }
    }

    // ─── PlayerSettings ───────────────────────────────────────────────────────

    private static void ApplyPlayerSettings(BuildConfig config, BuildTarget target, bool bump)
    {
        // Use the identity block for the active environment
        var id = config.ActiveIdentity;

        PlayerSettings.productName           = id.appName;
        PlayerSettings.applicationIdentifier = id.bundleId;
        PlayerSettings.bundleVersion         = id.versionName;
        PlayerSettings.Android.bundleVersionCode = id.versionCode;

        if (target == BuildTarget.Android && !string.IsNullOrEmpty(config.keystorePath))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName      = config.keystorePath;
            PlayerSettings.Android.keystorePass      = config.keystorePass;
            PlayerSettings.Android.keyaliasName      = config.keyAlias;
            PlayerSettings.Android.keyaliasPass      = config.keyPass;
        }

        // Auto-increment only when triggering an actual build
        if (bump)
        {
            id.versionCode++;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($""[Build] {config.activeEnvironment} identity applied — "" +
                      $""{id.bundleId} v{id.versionName} (build {id.versionCode})."");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static BuildConfig LoadBuildConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<BuildConfig>(BuildConfigPath);
        if (config == null)
            Debug.LogError($""[Build] BuildConfig not found at {BuildConfigPath}. Install Build Scripts module first."");
        return config;
    }
}
";
}
