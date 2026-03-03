using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;
using System.Collections.Generic;

public class UltimateCardFixer
{
    [MenuItem("Tools/Fix All Cards and Layout")]
    public static void Fix()
    {
        // 1. シーン内の HorizontalLayoutGroup を修正
        var allHlg = Object.FindObjectsOfType<HorizontalLayoutGroup>(true);
        foreach (var hlg in allHlg)
        {
            if (hlg.gameObject.name.Contains("HandPanel") || hlg.gameObject.name.Contains("HandContainer"))
            {
                if (!EditorUtility.IsPersistent(hlg))
                    Undo.RecordObject(hlg, "Fix Layout Group");
                
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = -30f;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                
                EditorUtility.SetDirty(hlg);
                Debug.Log($"Fixed LayoutGroup: {hlg.gameObject.name}");
            }
        }

        // 2. シーン内の全 CardUI インスタンスを修正
        var allCards = Object.FindObjectsOfType<CardUI>(true);
        foreach (var card in allCards)
        {
            if (card != null)
                FixCardInstance(card.gameObject, false);
        }

        // 3. プレハブの CardUI を修正
        string[] cardPrefabGuids = AssetDatabase.FindAssets("t:Prefab Card");
        HashSet<string> processedPaths = new HashSet<string>();
        foreach (var guid in cardPrefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (processedPaths.Contains(path)) continue;
            processedPaths.Add(path);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<CardUI>() != null)
            {
                FixCardInstance(prefab, true);
                EditorUtility.SetDirty(prefab);
                Debug.Log($"Fixed Card Prefab: {path}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Ultimate Card Fix Completed Successfully!");
    }

    private static void FixCardInstance(GameObject go, bool isPrefab)
    {
        if (go == null) return;

        // Parent Transform & All Children Scale
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (!isPrefab) Undo.RecordObject(t, "Fix Scale");
            t.localScale = Vector3.one;
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(100, 140);
        }

        // LayoutElement
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        
        if (le != null)
        {
            if (!isPrefab) Undo.RecordObject(le, "Fix Layout Element");
            le.preferredWidth = 100f;
            le.preferredHeight = 140f;
            le.minWidth = 100f;
            le.minHeight = 140f;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;
        }

        // Image
        Image img = go.GetComponent<Image>();
        if (img == null) img = go.GetComponentInChildren<Image>();
        if (img != null)
        {
            if (!isPrefab) Undo.RecordObject(img, "Fix Image");
            img.preserveAspect = false;
            RectTransform imgRt = img.GetComponent<RectTransform>();
            if (img.gameObject != go && imgRt != null)
            {
                imgRt.anchorMin = Vector2.zero;
                imgRt.anchorMax = Vector2.one;
                imgRt.offsetMin = Vector2.zero;
                imgRt.offsetMax = Vector2.zero;
            }
        }
    }
}
