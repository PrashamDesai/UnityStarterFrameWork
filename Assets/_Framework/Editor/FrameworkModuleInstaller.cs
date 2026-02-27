using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared helper utilities used by all module installer classes.
/// Provides folder creation, file writing, ScriptableObject creation,
/// and existence checks so every module installer stays concise.
/// </summary>
public static class FrameworkModuleInstaller
{
    // ─── Folder helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a directory (and all parents) relative to the project root.
    /// Safe to call even if the directory already exists.
    /// </summary>
    /// <param name="relativePath">e.g. "Assets/_Framework/Authentication"</param>
    public static void EnsureFolder(string relativePath)
    {
        string full = Path.Combine(Application.dataPath, "..", relativePath);
        full = Path.GetFullPath(full);
        if (!Directory.Exists(full))
        {
            Directory.CreateDirectory(full);
            AssetDatabase.ImportAsset(relativePath);
        }
    }

    // ─── Script helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Writes a C# script to the given path only if it does not already exist.
    /// </summary>
    /// <param name="assetPath">Full "Assets/…" path including filename and .cs</param>
    /// <param name="content">Full source code to write</param>
    public static void WriteScript(string assetPath, string content)
    {
        string full = Path.GetFullPath(assetPath);
        if (File.Exists(full))
        {
            Debug.Log($"[Framework] Skipped (already exists): {assetPath}");
            return;
        }
        File.WriteAllText(full, content);
        Debug.Log($"[Framework] ✓ Created: {assetPath}");
    }

    // ─── ScriptableObject helpers ─────────────────────────────────────────────

    /// <summary>
    /// Reflection-based ScriptableObject creator.
    /// Looks up the type by name at runtime so Editor installers never need a
    /// compile-time reference to runtime types that haven't been written yet.
    /// Resolves from "Assembly-CSharp" first, then "Assembly-CSharp-Editor".
    /// Returns the created asset, or null if the type isn't compiled yet
    /// (safe to call via EditorApplication.delayCall after AssetDatabase.Refresh).
    /// </summary>
    /// <param name="typeName">Simple class name, e.g. "AdsConfig"</param>
    /// <param name="assetPath">Full "Assets/…" path including filename and .asset</param>
    public static ScriptableObject CreateScriptableObject(string typeName, string assetPath)
    {
        // Try both assemblies (runtime SO classes live in Assembly-CSharp)
        var type = System.Type.GetType($"{typeName}, Assembly-CSharp")
                ?? System.Type.GetType($"{typeName}, Assembly-CSharp-Editor")
                ?? System.Type.GetType(typeName);

        if (type == null)
        {
            Debug.LogWarning($"[Framework] Type '{typeName}' not found yet — will retry after next recompile.");
            return null;
        }

        // Skip if asset already exists
        var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
        if (existing != null)
        {
            Debug.Log($"[Framework] Skipped SO (already exists): {assetPath}");
            return existing;
        }

        var so = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(so, assetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Framework] ✓ Created SO: {assetPath}");
        return so;
    }

    // ─── Existence check helpers ──────────────────────────────────────────────

    /// <summary>Returns true if the given asset path exists on disk.</summary>
    public static bool FileExists(string assetPath)
    {
        return File.Exists(Path.GetFullPath(assetPath));
    }

    /// <summary>Returns true if the given folder path exists on disk.</summary>
    public static bool FolderExists(string relativePath)
    {
        string full = Path.Combine(Application.dataPath, "..", relativePath);
        return Directory.Exists(Path.GetFullPath(full));
    }

    // ─── Scene hierarchy helpers ──────────────────────────────────────────────

    /// <summary>
    /// Creates a root-level partition header GameObject named "─── {label} ───"
    /// (visible as a visual separator in the Hierarchy panel).
    /// Safe to call if the object already exists — skips creation silently.
    /// </summary>
    /// <param name="label">Module label, e.g. "Ads" → produces "─── Ads ───"</param>
    public static GameObject CreateSceneHeader(string label)
    {
        string name = $"------ {label} ------";
        // Skip if already present in the scene
        var existing = GameObject.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(null); // root level, no parent
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return go;
    }

    /// <summary>
    /// Creates a root-level GameObject named <paramref name="goName"/> and attaches
    /// the component identified by <paramref name="componentTypeName"/> via reflection.
    /// The new object is a sibling of (not a child of) the partition header.
    /// Skips creation if a GameObject with that name already exists at the root.
    /// </summary>
    /// <param name="goName">Name for the new GameObject, e.g. "AdsManager"</param>
    /// <param name="componentTypeName">Simple class name, e.g. "AdsManager"</param>
    public static GameObject CreateSceneManager(string goName, string componentTypeName)
    {
        // Skip if already in scene
        var existing = GameObject.Find(goName);
        if (existing != null)
        {
            Debug.Log($"[Framework] Skipped (already in scene): {goName}");
            return existing;
        }

        var go = new GameObject(goName);
        go.transform.SetParent(null); // always a root sibling

        // Attach component via reflection (type may not be resolvable until after compile)
        var type = System.Type.GetType($"{componentTypeName}, Assembly-CSharp")
                ?? System.Type.GetType(componentTypeName);

        if (type != null)
            go.AddComponent(type);
        else
            Debug.LogWarning($"[Framework] Component '{componentTypeName}' not found yet — GameObject '{goName}' created without it. Reopen the scene after compilation.");

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[Framework] ✓ Scene object created: {goName}");
        return go;
    }
}

