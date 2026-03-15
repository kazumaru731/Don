using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class FixGameUIController : MonoBehaviour
{
    void Start()
    {
        string path = @"d:\Unity_projects\Don\Assets\Scripts\UI\GameUIController.cs";
        if (!File.Exists(path)) { Debug.LogError("File not found"); return; }

        string content = File.ReadAllText(path);

        // 誤って挿入されたブロックを探して削除
        // if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi) の中身を正規化
        string pattern = @"if \(DonFusionManager2D\.Instance\.IsWaitingForDonGaeshi\)\s*\{\s*DonFusionManager2D\.Instance\.RPC_DeclareDonGaeshi\(DonFusionManager2D\.Instance\.Runner\.LocalPlayer\);\s*if \(fm\.PlayerCredits\.TryGet\(localPlayerId, out var credits\)\) \{ \} else credits = 0;[\s\S]*?\}\s*\}";
        
        string correctInner = "if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi)\r\n                            {\r\n                                DonFusionManager2D.Instance.RPC_DeclareDonGaeshi(DonFusionManager2D.Instance.Runner.LocalPlayer);\r\n                            }";
        
        // 非常に具体的な置換を試みる
        string targetPart = @"if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi)
                            {
                                DonFusionManager2D.Instance.RPC_DeclareDonGaeshi(DonFusionManager2D.Instance.Runner.LocalPlayer);
                                        if (fm.PlayerCredits.TryGet(localPlayerId, out var credits)) { } else credits = 0;
            string scoreInfo = $""RD {fm.CurrentRound}/5 | {credits} Credits"";
            if (fm.DrawPenaltyCount > 0) scoreInfo += $"" | Penalty: +{fm.DrawPenaltyCount}"";
            penaltyText.text = scoreInfo;

            // ラウンド表示とターン表示の位置を下げて手札との被りを防ぐ
            if (penaltyText != null)
            {
                var rtP = penaltyText.GetComponent<RectTransform>();
                if (rtP != null)
                {
                    rtP.anchorMin = rtP.anchorMax = new Vector2(0.5f, 1f);
                    rtP.pivot = new Vector2(0.5f, 1f);
                    rtP.anchoredPosition = new Vector2(0f, -220f); 
                }
            }
            if (statusText != null)
            {
                var rtS = statusText.GetComponent<RectTransform>();
                if (rtS != null)
                {
                    rtS.anchorMin = rtS.anchorMax = new Vector2(0.5f, 1f);
                    rtS.pivot = new Vector2(0.5f, 1f);
                    rtS.anchoredPosition = new Vector2(0f, -300f);
                }
            }
                            }";

        content = content.Replace(targetPart, correctInner);

        // 正しい位置に挿入
        string anchor = @"penaltyText.text = scoreInfo;";
        string layoutCode = @"
            // ラウンド表示とターン表示の位置を下げて手札との被りを防ぐ
            if (penaltyText != null)
            {
                var rtP = penaltyText.GetComponent<RectTransform>();
                if (rtP != null)
                {
                    rtP.anchorMin = rtP.anchorMax = new Vector2(0.5f, 1f);
                    rtP.pivot = new Vector2(0.5f, 1f);
                    rtP.anchoredPosition = new Vector2(0f, -220f);
                }
            }
            if (statusText != null)
            {
                var rtS = statusText.GetComponent<RectTransform>();
                if (rtS != null)
                {
                    rtS.anchorMin = rtS.anchorMax = new Vector2(0.5f, 1f);
                    rtS.pivot = new Vector2(0.5f, 1f);
                    rtS.anchoredPosition = new Vector2(0f, -300f);
                }
            }";

        // コンテキストを絞って置換（メソッド内の適切な場所）
        int lastPenaltyIdx = content.LastIndexOf(anchor);
        if (lastPenaltyIdx != -1)
        {
            content = content.Insert(lastPenaltyIdx + anchor.Length, layoutCode);
        }

        File.WriteAllText(path, content);
        Debug.Log("GameUIController fixed via script.");
    }
}
