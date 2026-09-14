using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  PoolControllerEditor
//  Assets/2.Prefabs/{PoolType}/ folder all prefabs
//  auto-load into each ObjectPool.Prefabs list
// ============================================================

[CustomEditor(typeof(PoolController))]
public class PoolControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);

        if (GUILayout.Button("Load Prefabs From Folder", GUILayout.Height(32)))
        {
            LoadPrefabsFromFolders((PoolController)target);
        }

        GUI.backgroundColor = Color.white;
    }

    static void LoadPrefabsFromFolders(PoolController controller)
    {
        bool changed = false;

        foreach (PoolType type in Enum.GetValues(typeof(PoolType)))
        {
            var pool = controller.Pools.Find(p => p != null && p.Type == type);
            if (pool == null)
            {
                Debug.LogWarning($"[PoolControllerEditor] No ObjectPool found for type: {type}");
                continue;
            }

            string folderPath = $"Assets/_project/2.Prefabs/{type}";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[PoolControllerEditor] Folder not found: {folderPath}");
                continue;
            }

            string[] guids   = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var      prefabs = new List<GameObject>(guids.Length);

            foreach (string guid in guids)
            {
                string     path   = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    prefabs.Add(prefab);
            }

            Undo.RecordObject(pool, $"Load {type} Prefabs");
            pool.Prefabs = prefabs;
            EditorUtility.SetDirty(pool);
            changed = true;

            Debug.Log($"[PoolControllerEditor] {type}: {prefabs.Count} prefabs loaded from {folderPath}");
        }

        if (changed)
            AssetDatabase.SaveAssets();
    }
}
