#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds a Unity editor menu item that creates the complete bucket setup.
/// This script must stay inside an Editor folder because it uses UnityEditor code.
/// </summary>
public static class BucketSetupMenu
{
    [MenuItem("GameObject/Bucket Paint/Create Bucket Setup", false, 10)]
    private static void CreateBucketSetup()
    {
        GameObject root = new GameObject("BucketSetup");

        Undo.RegisterCreatedObjectUndo(root, "Create Bucket Setup");

        root.AddComponent<BucketMass>();
        root.AddComponent<BucketPhysics>();
        root.AddComponent<BucketView>();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }
}
#endif
