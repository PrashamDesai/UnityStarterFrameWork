using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom EditorWindow that acts as a one-stop dashboard for setting up
/// the reusable game framework modules in any Unity project.
/// Open via: Tools ▶ Framework Setup
/// </summary>
public class FrameworkSetupWindow : EditorWindow
{
    // ─── Scroll state ────────────────────────────────────────────────────────
    private Vector2 _scroll;

    // ─── Module definitions ──────────────────────────────────────────────────
    /// <summary>Descriptor for each framework module shown in the window.</summary>
    private struct Module
    {
        public string Title;
        public string Description;
        public string Icon;                    // Unicode icon shown in button
        public System.Action InstallAction;    // Called when button is clicked
        public System.Func<bool> IsInstalled;  // Returns true if already set up
    }

    private Module[] _modules;

    // ─── Styles (built lazily after GUI skin is ready) ──────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _moduleBoxStyle;
    private GUIStyle _descStyle;
    private GUIStyle _installButtonStyle;
    private GUIStyle _installedLabelStyle;
    private bool     _stylesBuilt;

    // ─── Colors ──────────────────────────────────────────────────────────────
    private static readonly Color AccentColor    = new Color(0.29f, 0.56f, 1f);
    private static readonly Color InstalledColor = new Color(0.28f, 0.75f, 0.45f);
    private static readonly Color CardColor      = new Color(0.18f, 0.18f, 0.22f);

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Framework Setup", priority = 0)]
    public static void Open()
    {
        var window = GetWindow<FrameworkSetupWindow>("Framework Setup");
        window.minSize = new Vector2(420, 600);
        window.Show();
    }

    private void OnEnable()
    {
        // Register all modules with their installer delegates and detection checks
        _modules = new[]
        {
            new Module
            {
                Title         = "Authentication",
                Description   = "Firebase Auth — Guest, Google & Apple Sign-In. Wraps sign-in, sign-out and account deletion with events.",
                Icon          = "🔐",
                InstallAction = AuthInstaller.Install,
                IsInstalled   = AuthInstaller.IsInstalled
            },
            new Module
            {
                Title         = "Ads",
                Description   = "MAX / AdMob integration with Banner, Interstitial and Rewarded support. Driven by an AdsConfig ScriptableObject.",
                Icon          = "📢",
                InstallAction = AdsInstaller.Install,
                IsInstalled   = AdsInstaller.IsInstalled
            },
            new Module
            {
                Title         = "Build Scripts",
                Description   = "One-click Android & iOS builds via BuildConfig ScriptableObject. Auto-increments version code.",
                Icon          = "🔨",
                InstallAction = BuildInstaller.Install,
                IsInstalled   = BuildInstaller.IsInstalled
            },
            new Module
            {
                Title         = "Sound & Haptics",
                Description   = "Audio pool, music player and platform haptics (Android vibration + iOS Taptic). Driven by SoundConfig SO.",
                Icon          = "🔊",
                InstallAction = SoundHapticsInstaller.Install,
                IsInstalled   = SoundHapticsInstaller.IsInstalled
            },
            new Module
            {
                Title         = "Settings & Links",
                Description   = "PlayerPrefs-backed settings (sound, haptics, notifications) + GameLinks ScriptableObject for store/policy URLs.",
                Icon          = "⚙️",
                InstallAction = SettingsLinksInstaller.Install,
                IsInstalled   = SettingsLinksInstaller.IsInstalled
            },
            new Module
            {
                Title         = "Firebase Firestore",
                Description   = "Firestore singleton — async Set / Get / UpdateFields / Delete / Listen / QueryWhere. Add FIREBASE_FIRESTORE define to activate.",
                Icon          = "🔥",
                InstallAction = FirebaseInstaller.Install,
                IsInstalled   = FirebaseInstaller.IsInstalled
            },
        };
    }

    private void OnGUI()
    {
        BuildStyles();
        DrawHeader();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        GUILayout.Space(8);

        foreach (var module in _modules)
            DrawModuleCard(module);

        GUILayout.Space(12);
        EditorGUILayout.EndScrollView();
        DrawFooter();
    }

    // ─── Drawing helpers ─────────────────────────────────────────────────────

    private void DrawHeader()
    {
        // Dark banner at the top
        var bannerRect = new Rect(0, 0, position.width, 72);
        EditorGUI.DrawRect(bannerRect, new Color(0.12f, 0.12f, 0.16f));

        GUILayout.Space(12);
        GUILayout.Label("⚡  StarterFramework", _headerStyle);
        GUILayout.Label("Click a module to scaffold it into your project", _subHeaderStyle);
        GUILayout.Space(12);

        // Divider line
        var divRect = GUILayoutUtility.GetRect(position.width, 1);
        EditorGUI.DrawRect(divRect, new Color(0.3f, 0.3f, 0.38f));
    }

    private void DrawModuleCard(Module module)
    {
        bool installed = module.IsInstalled();

        // Card background
        GUILayout.BeginVertical(_moduleBoxStyle);
        {
            GUILayout.BeginHorizontal();
            {
                // Icon + title column
                GUILayout.BeginVertical();
                {
                    GUILayout.Label($"{module.Icon}  {module.Title}", EditorStyles.boldLabel);
                    GUILayout.Space(2);
                    GUILayout.Label(module.Description, _descStyle);
                }
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // Action column
                GUILayout.BeginVertical(GUILayout.Width(110));
                {
                    GUILayout.Space(6);
                    if (installed)
                    {
                        GUILayout.Label("✓  Installed", _installedLabelStyle);
                    }
                    else
                    {
                        if (GUILayout.Button("Install", _installButtonStyle, GUILayout.Width(100), GUILayout.Height(32)))
                        {
                            module.InstallAction?.Invoke();
                            AssetDatabase.Refresh();
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUILayout.Space(6);
    }

    private void DrawFooter()
    {
        var rect = new Rect(0, position.height - 24, position.width, 24);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.16f));
        GUI.Label(new Rect(8, position.height - 22, position.width - 16, 20),
                  "Assets/_Framework  |  Tools > Framework Setup",
                  EditorStyles.centeredGreyMiniLabel);
    }

    // ─── Lazy style builder ─────────────────────────────────────────────────

    private void BuildStyles()
    {
        if (_stylesBuilt) return;
        _stylesBuilt = true;

        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 18,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };

        _subHeaderStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.UpperCenter,
            normal    = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        _moduleBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 10, 10),
            margin  = new RectOffset(10, 10, 0, 0),
            normal  = { background = MakeTexture(2, 2, CardColor) }
        };

        _descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 10,
            normal   = { textColor = new Color(0.72f, 0.72f, 0.72f) },
            wordWrap = true
        };

        _installButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12,
            normal    = { textColor = Color.white, background = MakeTexture(2, 2, AccentColor) },
            hover     = { textColor = Color.white, background = MakeTexture(2, 2, new Color(0.4f, 0.65f, 1f)) },
            active    = { textColor = Color.white, background = MakeTexture(2, 2, new Color(0.2f, 0.46f, 0.9f)) }
        };

        _installedLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal   = { textColor = InstalledColor },
            alignment = TextAnchor.MiddleCenter
        };
    }

    /// <summary>Creates a solid-color 2x2 Texture2D (used for button backgrounds).</summary>
    private static Texture2D MakeTexture(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
