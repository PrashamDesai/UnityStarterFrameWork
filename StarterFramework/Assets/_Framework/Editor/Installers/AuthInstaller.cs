using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Authentication module.
/// Creates the Authentication folder and writes the AuthManager runtime script.
/// Called by FrameworkSetupWindow when the user clicks "Install".
/// </summary>
public static class AuthInstaller
{
    private const string FolderPath = "Assets/_Framework/Authentication";
    private const string ScriptPath = "Assets/_Framework/Authentication/AuthManager.cs";

    // ─── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns true if AuthManager.cs has already been installed.</summary>
    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(ScriptPath);

    // ─── Install ──────────────────────────────────────────────────────────────

    /// <summary>Scaffolds the Authentication module.</summary>
    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(ScriptPath, AuthManagerTemplate());
        // Defer scene setup until after Unity compiles the new scripts
        EditorApplication.delayCall += SetupScene;
        AssetDatabase.Refresh();
        Debug.Log("[Framework] ✅ Authentication module installed.");
    }

    private static void SetupScene()
    {
        FrameworkModuleInstaller.CreateSceneHeader("Authentication");
        FrameworkModuleInstaller.CreateSceneManager("AuthManager", "AuthManager");
    }

    // ─── Template ─────────────────────────────────────────────────────────────

    private static string AuthManagerTemplate() => @"using System;
using System.Threading.Tasks;
using UnityEngine;

#if FIREBASE_AUTH
using Firebase;
using Firebase.Auth;
using Google;
#endif

/// <summary>
/// Singleton that wraps Firebase Authentication.
/// Supports anonymous (guest), Google and Apple sign-in flows.
/// Compile-guard: add FIREBASE_AUTH to Scripting Define Symbols to enable Firebase calls.
/// </summary>
public class AuthManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static AuthManager Instance { get; private set; }

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired whenever the Firebase auth state changes (login / logout).</summary>
    public static event Action<string> OnAuthStateChanged;   // userId or null
    /// <summary>Fired on successful sign-in with the user's display name.</summary>
    public static event Action<string> OnSignInSuccess;
    /// <summary>Fired when any sign-in attempt fails.</summary>
    public static event Action<string> OnSignInFailed;

#if FIREBASE_AUTH
    private FirebaseAuth _auth;
    private FirebaseUser CurrentUser => _auth?.CurrentUser;

    [Header(""Google Sign-In"")]
    [Tooltip(""Web Client ID from Firebase Console > Project Settings > SHA"")]
    [SerializeField] private string _googleWebClientId = ""YOUR_WEB_CLIENT_ID.apps.googleusercontent.com"";
#endif

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
#if FIREBASE_AUTH
        InitFirebase();
#endif
    }

    private void OnDestroy()
    {
#if FIREBASE_AUTH
        if (_auth != null) _auth.StateChanged -= HandleAuthStateChanged;
#endif
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

#if FIREBASE_AUTH
    private async void InitFirebase()
    {
        var status = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (status != DependencyStatus.Available)
        {
            Debug.LogError($""[Auth] Firebase dependency check failed: {status}"");
            return;
        }
        _auth = FirebaseAuth.DefaultInstance;
        _auth.StateChanged += HandleAuthStateChanged;
    }

    private void HandleAuthStateChanged(object sender, EventArgs e)
    {
        OnAuthStateChanged?.Invoke(_auth.CurrentUser?.UserId);
    }
#endif

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Signs in anonymously (creates a guest account).</summary>
    public async Task SignInAnonymously()
    {
#if FIREBASE_AUTH
        try
        {
            var result = await _auth.SignInAnonymouslyAsync();
            OnSignInSuccess?.Invoke(""Guest"");
            Debug.Log($""[Auth] Signed in anonymously: {result.User.UserId}"");
        }
        catch (Exception ex)
        {
            OnSignInFailed?.Invoke(ex.Message);
            Debug.LogError($""[Auth] Anonymous sign-in failed: {ex.Message}"");
        }
#else
        Debug.LogWarning(""[Auth] FIREBASE_AUTH not defined. Add it to Scripting Define Symbols."");
        await Task.CompletedTask;
#endif
    }

    /// <summary>Initiates Google Sign-In flow and links the credential to Firebase.</summary>
    public async Task SignInWithGoogle()
    {
#if FIREBASE_AUTH
        try
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId      = _googleWebClientId,
                RequestIdToken   = true,
                RequestEmail     = true
            };
            var googleUser = await GoogleSignIn.DefaultInstance.SignIn();
            var credential  = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
            var result       = await _auth.SignInWithCredentialAsync(credential);
            OnSignInSuccess?.Invoke(result.User.DisplayName);
        }
        catch (Exception ex)
        {
            OnSignInFailed?.Invoke(ex.Message);
            Debug.LogError($""[Auth] Google sign-in failed: {ex.Message}"");
        }
#else
        Debug.LogWarning(""[Auth] FIREBASE_AUTH not defined."");
        await Task.CompletedTask;
#endif
    }

    /// <summary>Signs out the currently authenticated user.</summary>
    public void SignOut()
    {
#if FIREBASE_AUTH
        _auth?.SignOut();
        Debug.Log(""[Auth] Signed out."");
#endif
    }

    /// <summary>Permanently deletes the current user's Firebase account.</summary>
    public async Task DeleteAccount()
    {
#if FIREBASE_AUTH
        try
        {
            await _auth.CurrentUser?.DeleteAsync();
            Debug.Log(""[Auth] Account deleted."");
        }
        catch (Exception ex)
        {
            Debug.LogError($""[Auth] Delete account failed: {ex.Message}"");
        }
#else
        await Task.CompletedTask;
#endif
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if a user is currently signed in.</summary>
    public bool IsSignedIn
    {
#if FIREBASE_AUTH
        get => _auth?.CurrentUser != null;
#else
        get => false;
#endif
    }

    /// <summary>The current user's UID, or null if not signed in.</summary>
    public string UserId
    {
#if FIREBASE_AUTH
        get => _auth?.CurrentUser?.UserId;
#else
        get => null;
#endif
    }
}
";
}
