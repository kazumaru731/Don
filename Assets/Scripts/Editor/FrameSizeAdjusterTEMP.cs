using UnityEngine;
using UnityEditor;

public class FrameSizeAdjuster {
    [MenuItem("Tools/DonGame/Adjust Frame Size")]
    public static void Adjust() {
        var allRects = Object.FindObjectsOfType<RectTransform>(true);
        int changed = 0;
        foreach (var rt in allRects) {
            if (rt.gameObject.name == "DiscardBorder" || rt.gameObject.name == "DiscardPilePanel") {
                Undo.RecordObject(rt, "Adjust Border Size");
                
                // カードサイズ(100x140)より周囲に+10px程度の余白を持たせる
                rt.sizeDelta = new Vector2(120f, 160f); 
                EditorUtility.SetDirty(rt);
                changed++;
                Debug.Log($"Resized {rt.gameObject.name} in scene to {rt.sizeDelta}");
            }
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) {
                bool prefabChanged = false;
                var rts = prefab.GetComponentsInChildren<RectTransform>(true);
                foreach (var rt in rts) {
                    if (rt.gameObject.name == "DiscardBorder" || rt.gameObject.name == "DiscardPilePanel") {
                        rt.sizeDelta = new Vector2(120f, 160f);
                        prefabChanged = true;
                        Debug.Log($"Resized {rt.gameObject.name} in prefab {path}");
                    }
                }
                if (prefabChanged) EditorUtility.SetDirty(prefab);
            }
        }

        // GameUIBuilder.cs の生成コードも修正しておく
        string builderPath = "Assets/Scripts/Editor/GameUIBuilder.cs";
        string content = System.IO.File.ReadAllText(builderPath);
        bool builderChanged = false;
        
        // 以前の変更元 (108, 148) を探す
        if (content.Contains("borderRT.sizeDelta = new Vector2(108, 148);")) {
            content = content.Replace("borderRT.sizeDelta = new Vector2(108, 148);", "borderRT.sizeDelta = new Vector2(120, 160);");
            builderChanged = true;
        }
        if (content.Contains("discardRT.sizeDelta = new Vector2(108, 148);")) {
            content = content.Replace("discardRT.sizeDelta = new Vector2(108, 148);", "discardRT.sizeDelta = new Vector2(120, 160);");
            builderChanged = true;
        }
        if (builderChanged) {
            System.IO.File.WriteAllText(builderPath, content);
            Debug.Log("Updated GameUIBuilder.cs default sizes");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Adjustment completed! Changed {changed} scene objects.");
    }
}
