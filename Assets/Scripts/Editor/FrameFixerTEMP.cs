using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class FrameFixer {
    [MenuItem("Tools/DonGame/Fix Frame and Glow")]
    public static void Fix() {
        // 1. ぼかし無し画像を再生成
        GenerateRoundedFrame.Generate();

        // 2. シーン内の DiscardBorder と DiscardPilePanel のサイズを修正
        var allRects = Object.FindObjectsOfType<RectTransform>(true);
        int changed = 0;
        foreach (var rt in allRects) {
            if (rt.gameObject.name == "DiscardBorder" || rt.gameObject.name == "DiscardPilePanel") {
                Undo.RecordObject(rt, "Fix Border Size");
                
                // カードサイズ(100x140)より少しだけ大きい枠 (108x148 程度)
                rt.sizeDelta = new Vector2(108f, 148f);
                EditorUtility.SetDirty(rt);
                changed++;
                Debug.Log($"Resized {rt.gameObject.name} in scene to {rt.sizeDelta}");
            }
        }

        // 3. プレハブ内の DiscardBorder なども修正（もし含まれていれば）
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) {
                bool prefabChanged = false;
                var rts = prefab.GetComponentsInChildren<RectTransform>(true);
                foreach (var rt in rts) {
                    if (rt.gameObject.name == "DiscardBorder" || rt.gameObject.name == "DiscardPilePanel") {
                        rt.sizeDelta = new Vector2(108f, 148f);
                        prefabChanged = true;
                        Debug.Log($"Resized {rt.gameObject.name} in prefab {path}");
                    }
                }
                if (prefabChanged) EditorUtility.SetDirty(prefab);
            }
        }

        // 4. GameUIBuilder.cs の生成コードも修正しておく
        string builderPath = "Assets/Scripts/Editor/GameUIBuilder.cs";
        string content = System.IO.File.ReadAllText(builderPath);
        bool builderChanged = false;
        if (content.Contains("borderRT.sizeDelta = new Vector2(250, 350);")) {
            content = content.Replace("borderRT.sizeDelta = new Vector2(250, 350);", "borderRT.sizeDelta = new Vector2(108, 148);");
            builderChanged = true;
        }
        if (content.Contains("discardRT.sizeDelta = new Vector2(250, 350);")) {
            content = content.Replace("discardRT.sizeDelta = new Vector2(250, 350);", "discardRT.sizeDelta = new Vector2(108, 148);");
            builderChanged = true;
        }
        if (builderChanged) {
            System.IO.File.WriteAllText(builderPath, content);
            Debug.Log("Updated GameUIBuilder.cs default sizes");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Fix completed! Changed {changed} scene objects.");
    }
}
