using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class UniversalFontReplacer : EditorWindow
{
    private Font targetFontRegular;
    private Font targetFontBold;

    [MenuItem("Don Game/Universal Font Replacer")]
    public static void ShowWindow()
    {
        GetWindow<UniversalFontReplacer>("Font Replacer");
    }

    private void OnEnable()
    {
        // デフォルトで Zen Kaku Gothic New を探す
        targetFontRegular = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Zen_Kaku_Gothic_New/ZenKakuGothicNew-Regular.ttf");
        targetFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Zen_Kaku_Gothic_New/ZenKakuGothicNew-Bold.ttf");
    }

    private void OnGUI()
    {
        GUILayout.Label("Universal Font Replacer", EditorStyles.boldLabel);
        
        targetFontRegular = (Font)EditorGUILayout.ObjectField("Regular Font", targetFontRegular, typeof(Font), false);
        targetFontBold = (Font)EditorGUILayout.ObjectField("Bold Font", targetFontBold, typeof(Font), false);

        if (GUILayout.Button("Replace All Fonts in Current Scene"))
        {
            ReplaceInCurrentScene();
        }

        if (GUILayout.Button("Replace All Fonts in Project Prefabs"))
        {
            ReplaceInAllPrefabs();
        }
    }

    private void ReplaceInCurrentScene()
    {
        if (targetFontRegular == null) { EditorUtility.DisplayDialog("Error", "Select a Regular font.", "OK"); return; }

        Text[] texts = Resources.FindObjectsOfTypeAll<Text>();
        int count = 0;

        foreach (Text t in texts)
        {
            // シーン内のオブジェクトのみ（プレハブ等は除外）
            if (EditorUtility.IsPersistent(t.transform.root.gameObject)) continue;
            
            Undo.RecordObject(t, "Replace Font");
            ApplyFont(t);
            count++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FontReplacer] Replaced {count} text elements in current scene.");
        EditorUtility.DisplayDialog("Success", $"Replaced {count} elements.", "OK");
    }

    private void ReplaceInAllPrefabs()
    {
        if (targetFontRegular == null) { EditorUtility.DisplayDialog("Error", "Select a Regular font.", "OK"); return; }

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Text[] texts = prefab.GetComponentsInChildren<Text>(true);
            if (texts.Length == 0) continue;

            bool changed = false;
            foreach (Text t in texts)
            {
                Undo.RecordObject(t, "Replace Font");
                ApplyFont(t);
                changed = true;
                count++;
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }
        }

        Debug.Log($"[FontReplacer] Replaced {count} text elements in all prefabs.");
        EditorUtility.DisplayDialog("Success", $"Replaced {count} elements in prefabs.", "OK");
    }

    private void ApplyFont(Text t)
    {
        // 既存のフォントスタイルを維持しつつ、BoldならBoldフォントを当てる
        if (t.fontStyle == FontStyle.Bold || t.fontStyle == FontStyle.BoldAndItalic)
        {
            if (targetFontBold != null) t.font = targetFontBold;
            else t.font = targetFontRegular;
        }
        else
        {
            t.font = targetFontRegular;
        }

        // 新しいフォントで文字が消えないように Overflow を設定
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
    }
}
