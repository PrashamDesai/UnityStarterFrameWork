using UnityEditor;
using UnityEngine;

/// <summary>
/// Installer for the Firebase Firestore module.
/// Creates the Firebase folder and writes FirebaseManager.cs.
/// Guarded by the FIREBASE_FIRESTORE scripting define symbol.
/// </summary>
public static class FirebaseInstaller
{
    private const string FolderPath   = "Assets/_Framework/Firebase";
    private const string ScriptPath   = "Assets/_Framework/Firebase/FirebaseManager.cs";

    // ─── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns true if FirebaseManager.cs has been installed.</summary>
    public static bool IsInstalled() => FrameworkModuleInstaller.FileExists(ScriptPath);

    // ─── Install ──────────────────────────────────────────────────────────────

    /// <summary>Scaffolds the Firebase Firestore module.</summary>
    public static void Install()
    {
        FrameworkModuleInstaller.EnsureFolder(FolderPath);
        FrameworkModuleInstaller.WriteScript(ScriptPath, FirebaseManagerTemplate());
        EditorApplication.delayCall += SetupScene;
        Debug.Log("[Framework] ✅ Firebase Firestore module installed.");
    }

    private static void SetupScene()
    {
        FrameworkModuleInstaller.CreateSceneHeader("Firebase Firestore");
        FrameworkModuleInstaller.CreateSceneManager("FirebaseManager", "FirebaseManager");
    }

    // ─── Template ─────────────────────────────────────────────────────────────

    private static string FirebaseManagerTemplate() => @"using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

#if FIREBASE_FIRESTORE
using Firebase;
using Firebase.Firestore;
#endif

/// <summary>
/// Singleton wrapper around Firebase Firestore.
/// Add FIREBASE_FIRESTORE to Scripting Define Symbols to activate Firebase calls.
/// Provides async Set / Get / UpdateField / Delete / Listen helpers.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static FirebaseManager Instance { get; private set; }

    // ─── State ────────────────────────────────────────────────────────────────
    public bool IsInitialised { get; private set; }

    // ─── Events ───────────────────────────────────────────────────────────────
    public static event Action         OnFirebaseReady;
    public static event Action<string> OnFirebaseError;

#if FIREBASE_FIRESTORE
    private FirebaseFirestore _firestore;
#endif

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
#if FIREBASE_FIRESTORE
        InitFirebase();
#else
        Debug.LogWarning(""[Firebase] Add FIREBASE_FIRESTORE to Scripting Define Symbols and import the Firebase Firestore SDK."");
#endif
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

#if FIREBASE_FIRESTORE
    private async void InitFirebase()
    {
        try
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                string err = $""Firebase dependency error: {status}"";
                Debug.LogError($""[Firebase] {err}"");
                OnFirebaseError?.Invoke(err);
                return;
            }
            _firestore    = FirebaseFirestore.DefaultInstance;
            IsInitialised = true;
            OnFirebaseReady?.Invoke();
            Debug.Log(""[Firebase] ✅ Firestore initialised successfully."");
        }
        catch (Exception ex)
        {
            Debug.LogError($""[Firebase] Init failed: {ex.Message}"");
            OnFirebaseError?.Invoke(ex.Message);
        }
    }
#endif

    // ─────────────────────────────────────────────────────────────────────────
    // FIRESTORE API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or overwrites a Firestore document.
    /// <typeparamref name=""T""/> is serialised to a string-keyed dictionary via JSON.
    /// </summary>
    public async Task SetDocument<T>(string collection, string documentId, T data)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) { Debug.LogWarning(""[Firebase] Not initialised yet.""); return; }
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(data));
            await _firestore.Collection(collection).Document(documentId).SetAsync(dict);
            Debug.Log($""[Firebase] SetDocument: {collection}/{documentId}"");
        }
        catch (Exception ex) { Debug.LogError($""[Firebase] SetDocument failed: {ex.Message}""); }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Retrieves a Firestore document and deserialises it as <typeparamref name=""T""/>.
    /// Returns default(T) if the document doesn't exist or on error.
    /// </summary>
    public async Task<T> GetDocument<T>(string collection, string documentId)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) return default;
        try
        {
            var snap = await _firestore.Collection(collection).Document(documentId).GetSnapshotAsync();
            if (!snap.Exists) return default;
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(snap.ToDictionary()));
        }
        catch (Exception ex)
        {
            Debug.LogError($""[Firebase] GetDocument failed: {ex.Message}"");
            return default;
        }
#else
        await Task.CompletedTask;
        return default;
#endif
    }

    /// <summary>
    /// Updates one or more specific fields in a document without touching other fields.
    /// Pass a flat dictionary of field-path → value pairs.
    /// </summary>
    public async Task UpdateFields(string collection, string documentId, Dictionary<string, object> fields)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) return;
        try
        {
            await _firestore.Collection(collection).Document(documentId).UpdateAsync(fields);
            Debug.Log($""[Firebase] UpdateFields: {collection}/{documentId}"");
        }
        catch (Exception ex) { Debug.LogError($""[Firebase] UpdateFields failed: {ex.Message}""); }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>Deletes a Firestore document completely.</summary>
    public async Task DeleteDocument(string collection, string documentId)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) return;
        try
        {
            await _firestore.Collection(collection).Document(documentId).DeleteAsync();
            Debug.Log($""[Firebase] DeleteDocument: {collection}/{documentId}"");
        }
        catch (Exception ex) { Debug.LogError($""[Firebase] DeleteDocument failed: {ex.Message}""); }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Attaches a real-time snapshot listener on a document.
    /// Call Dispose() on the returned handle to unsubscribe.
    /// </summary>
    public IDisposable ListenToDocument<T>(string collection, string documentId, Action<T> onUpdate)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) return null;
        return _firestore.Collection(collection).Document(documentId).Listen(snap =>
        {
            if (!snap.Exists) return;
            try
            {
                var result = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(snap.ToDictionary()));
                onUpdate?.Invoke(result);
            }
            catch (Exception ex) { Debug.LogError($""[Firebase] ListenToDocument parse failed: {ex.Message}""); }
        });
#else
        return null;
#endif
    }

    /// <summary>
    /// Queries a collection where <paramref name=""field""/> equals <paramref name=""value""/>.
    /// Returns a list of deserialised documents of type <typeparamref name=""T""/>.
    /// </summary>
    public async Task<List<T>> QueryWhere<T>(string collection, string field, object value)
    {
#if FIREBASE_FIRESTORE
        if (!IsInitialised) return new List<T>();
        try
        {
            var query = _firestore.Collection(collection).WhereEqualTo(field, value);
            var snap  = await query.GetSnapshotAsync();
            var list  = new List<T>();
            foreach (var doc in snap.Documents)
            {
                var item = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(doc.ToDictionary()));
                list.Add(item);
            }
            return list;
        }
        catch (Exception ex)
        {
            Debug.LogError($""[Firebase] QueryWhere failed: {ex.Message}"");
            return new List<T>();
        }
#else
        await Task.CompletedTask;
        return new List<T>();
#endif
    }
}
";
}
