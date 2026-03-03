using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;

public static class TempLayoutFixer
{
    [MenuItem("Tools/DonGame/Temp Fix Layout")]
    public static void FixLayout()
    {
        // 1. 重複キャンバスの削除 (インスタンスIDが最新のもの以外を削除するなどの安全な方法)
        var gameCanvases = GameObject.FindObjectsOfType<Canvas>();
        
        GameObject newestGameCanvas = null;
        GameObject newestTitleCanvas = null;
        
        foreach (var c in gameCanvases)
        {
            if (c.gameObject.name == "GameCanvas")
            {
                if (newestGameCanvas == null || c.gameObject.GetInstanceID() > newestGameCanvas.GetInstanceID())
                {
                    if (newestGameCanvas != null) Object.DestroyImmediate(newestGameCanvas);
                    newestGameCanvas = c.gameObject;
                }
                else
                {
                    Object.DestroyImmediate(c.gameObject);
                }
            }
            else if (c.gameObject.name == "TitleCanvas")
            {
                if (newestTitleCanvas == null || c.gameObject.GetInstanceID() > newestTitleCanvas.GetInstanceID())
                {
                    if (newestTitleCanvas != null) Object.DestroyImmediate(newestTitleCanvas);
                    newestTitleCanvas = c.gameObject;
                }
                else
                {
                    Object.DestroyImmediate(c.gameObject);
                }
            }
        }

        // 2. ソートボタンの移動
        if (newestGameCanvas != null)
        {
            var rankBtn = FindChildRecursive(newestGameCanvas.transform, "SortRankButton");
            if (rankBtn != null)
            {
                var rt = rankBtn.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(250, 45); // 手札の中に入るのではなく上に乗るように位置調整
            }

            var suitBtn = FindChildRecursive(newestGameCanvas.transform, "SortSuitButton");
            if (suitBtn != null)
            {
                var rt = suitBtn.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(400, 45); // 上に乗るように位置調整
            }

            // 3. OpponentInfoContainer が無ければ作成
            var oppContainer = FindChildRecursive(newestGameCanvas.transform, "OpponentInfoContainer");
            if (oppContainer == null)
            {
                GameObject oppContainerObj = new GameObject("OpponentInfoContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
                oppContainerObj.transform.SetParent(newestGameCanvas.transform, false);
                var oppRT = oppContainerObj.GetComponent<RectTransform>();
                oppRT.anchorMin = new Vector2(0, 0);
                oppRT.anchorMax = new Vector2(0, 1);
                oppRT.pivot = new Vector2(0, 0.5f);
                oppRT.sizeDelta = new Vector2(300, 0); // 高さStretch時はsizeDelta.yは無視される
                oppRT.anchoredPosition = new Vector2(20, 0); // 左から20pxの余白
                
                var oppVlg = oppContainerObj.GetComponent<VerticalLayoutGroup>();
                oppVlg.padding = new RectOffset(10, 10, 10, 10);
                oppVlg.spacing = 20;
                oppVlg.childAlignment = TextAnchor.UpperLeft;
                oppVlg.childControlHeight = false;
                oppVlg.childControlWidth = true;
                oppVlg.childForceExpandHeight = false;
                oppVlg.childForceExpandWidth = true;

                oppContainer = oppContainerObj.transform;
            }

            // 4. GameUIControllerへの紐付け
            var gameUIController = Object.FindObjectOfType<GameUIController>();
            if (gameUIController != null && oppContainer != null)
            {
                gameUIController.opponentInfoContainer = oppContainer;
                
                if (gameUIController.opponentInfoPrefab == null)
                {
                    string oppPrefabPath = "Assets/Prefabs/OpponentUIInfo.prefab";
                    GameObject oppPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(oppPrefabPath);
                    if (oppPrefab != null)
                    {
                        gameUIController.opponentInfoPrefab = oppPrefab;
                    }
                }
                
                EditorUtility.SetDirty(gameUIController);
            }
        }

        Debug.Log("TempLayoutFixer completed cleaning up duplicates and fixing layout!");
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
