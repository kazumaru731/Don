using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// シーン内のクリック可能なUI要素を全て診断するエディタツール（強化版）
/// </summary>
public class ClickDiagnostics
{
    [MenuItem("DonGame/Diagnose Click Targets")]
    public static void DiagnoseClickTargets()
    {
        Debug.Log("====== [ClickDiagnostics v2] 診断開始 ======");

        var gameUICtrl = Object.FindObjectOfType<DonGame2D.UI.GameUIController>();
        if (gameUICtrl == null)
        {
            Debug.LogWarning("GameUIController が見つかりません（Playモード中に実行してください）");
            return;
        }

        // --- DeckPilePanel の完全な親階層を確認 ---
        Debug.Log("\n====== DeckPilePanel の親階層チェック ======");
        Transform deckTr = gameUICtrl.deckPileContainer;
        CheckHierarchyForBlockers(deckTr, "DeckPile");

        // --- DiscardPilePanel の完全な親階層を確認 ---
        Debug.Log("\n====== DiscardPilePanel の親階層チェック ======");
        Transform discTr = gameUICtrl.discardPileContainer;
        CheckHierarchyForBlockers(discTr, "DiscardPile");

        // --- DeckPilePanel の子オブジェクトを詳細確認 ---
        Debug.Log($"\n====== DeckPilePanel 子オブジェクト詳細 (子数={deckTr?.childCount}) ======");
        if (deckTr != null)
        {
            foreach (Transform child in deckTr)
            {
                var rt = child.GetComponent<RectTransform>();
                var img = child.GetComponent<Image>();
                var btn = child.GetComponent<Button>();
                Debug.Log($"  [{child.GetSiblingIndex()}] {child.name} | active={child.gameObject.activeSelf} | btn={btn != null} | raycast={(img != null ? img.raycastTarget.ToString() : "no-img")} | size={rt?.sizeDelta}");
            }
        }

        // --- DiscardPilePanel の子オブジェクトを詳細確認 ---
        Debug.Log($"\n====== DiscardPilePanel 子オブジェクト詳細 (子数={discTr?.childCount}) ======");
        if (discTr != null)
        {
            foreach (Transform child in discTr)
            {
                var rt = child.GetComponent<RectTransform>();
                var img = child.GetComponent<Image>();
                var btn = child.GetComponent<Button>();
                Debug.Log($"  [{child.GetSiblingIndex()}] {child.name} | active={child.gameObject.activeSelf} | btn={btn != null} | raycast={(img != null ? img.raycastTarget.ToString() : "no-img")} | size={rt?.sizeDelta}");
            }
        }

        // --- 全Buttonの確認（動的リスナー含む） ---
        Debug.Log("\n====== 全Buttonリスト（アクティブのみ） ======");
        Button[] allBtns = Object.FindObjectsOfType<Button>(true);
        foreach (var btn in allBtns)
        {
            if (!btn.gameObject.activeInHierarchy) continue;
            var rt = btn.GetComponent<RectTransform>();
            var img = btn.GetComponent<Image>() ?? btn.GetComponentInChildren<Image>();
            string rayStr = img != null ? img.raycastTarget.ToString() : "no-img";
            string path = GetPath(btn.transform);
            Debug.Log($"  [BTN] {btn.gameObject.name} | interactable={btn.interactable} | raycast={rayStr} | path={path}");
        }

        // --- 全画面を覆うイメージを探す ---
        Debug.Log("\n====== 全画面サイズ（W>300,H>300）の raycastTarget=true Image ======");
        Graphic[] allGraphics = Object.FindObjectsOfType<Graphic>(false);
        foreach (var g in allGraphics)
        {
            if (!g.raycastTarget) continue;
            var rt = g.GetComponent<RectTransform>();
            if (rt == null) continue;
            Vector2 size = rt.rect.size;
            if (size.x > 300 || size.y > 300)
            {
                string path = GetPath(g.transform);
                Debug.Log($"  [大サイズ] {g.gameObject.name} | size={size:F0} | type={g.GetType().Name} | path={path}");
            }
        }

        Debug.Log("\n====== 診断完了 ======");
    }

    private static void CheckHierarchyForBlockers(Transform t, string label)
    {
        if (t == null) { Debug.LogWarning($"  {label}: null"); return; }

        // 自分自身から根まで登る
        Transform cur = t;
        int depth = 0;
        while (cur != null && depth < 10)
        {
            var img = cur.GetComponent<Image>();
            var cg = cur.GetComponent<CanvasGroup>();
            var rt = cur.GetComponent<RectTransform>();
            var canvas = cur.GetComponent<Canvas>();

            string issues = "";
            if (cg != null && !cg.interactable) issues += "[CanvasGroup interactable=FALSE] ";
            if (cg != null && !cg.blocksRaycasts) issues += "[CanvasGroup blocksRaycasts=FALSE] ";
            if (!cur.gameObject.activeInHierarchy) issues += "[GameObject Inactive] ";
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null) issues += "[Canvas no GraphicRaycaster] ";

            string status = string.IsNullOrEmpty(issues) ? "OK" : "⚠ " + issues;
            string imgStr = img != null ? $"img.raycast={img.raycastTarget}" : "no-img";
            string cgStr = cg != null ? $"interactable={cg.interactable},blocksRaycasts={cg.blocksRaycasts}" : "no-CanvasGroup";

            Debug.Log($"  [depth={depth}] {cur.name} | {status} | {imgStr} | {cgStr}");

            cur = cur.parent;
            depth++;
        }
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "";
        var parts = new System.Collections.Generic.List<string>();
        Transform cur = t;
        int i = 0;
        while (cur != null && i++ < 8)
        {
            parts.Insert(0, cur.name);
            cur = cur.parent;
        }
        return string.Join("/", parts);
    }
}
