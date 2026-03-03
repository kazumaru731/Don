using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;
using System.Text;

public class CardDetailDiagnostics
{
    [MenuItem("Tools/Debug/Detailed Card Diagnostics")]
    public static void RunDiagnostics()
    {
        var handPanel = GameObject.Find("PlayerHandPanel");
        if (handPanel == null)
        {
            Debug.LogError("PlayerHandPanelが見つかりません。ゲームを実行中、またはシーン内にパネルがある状態で実行してください。");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 手札パネルの状態 ===");
        var hlg = handPanel.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            sb.AppendLine($"HLG ChildControl: W={hlg.childControlWidth}, H={hlg.childControlHeight}");
            sb.AppendLine($"HLG ForceExpand: W={hlg.childForceExpandWidth}, H={hlg.childForceExpandHeight}");
            sb.AppendLine($"HLG Spacing: {hlg.spacing}");
        }

        sb.AppendLine("\n=== カードごとの詳細状態 ===");
        for (int i = 0; i < handPanel.transform.childCount; i++)
        {
            var child = handPanel.transform.GetChild(i);
            var rt = child.GetComponent<RectTransform>();
            var le = child.GetComponent<LayoutElement>();
            var cardUI = child.GetComponent<CardUI>();
            var img = child.GetComponent<Image>();
            if (img == null) img = child.GetComponentInChildren<Image>();

            sb.AppendLine($"[{i}] 名前: {child.name}");
            sb.AppendLine($"    Rect sizeDelta: {rt.sizeDelta}");
            sb.AppendLine($"    Local Scale: {child.localScale}");
            if (le != null)
            {
                sb.AppendLine($"    LayoutElement: Pref={le.preferredWidth}x{le.preferredHeight}, Min={le.minWidth}x{le.minHeight}, Flex={le.flexibleWidth}x{le.flexibleHeight}");
            }
            else
            {
                sb.AppendLine("    LayoutElement: なし");
            }
            if (img != null)
            {
                sb.AppendLine($"    Image Sprite: {img.sprite?.name} ({img.sprite?.rect.width}x{img.sprite?.rect.height})");
                sb.AppendLine($"    Image PreserveAspect: {img.preserveAspect}");
                sb.AppendLine($"    Image Rect sizeDelta: {img.GetComponent<RectTransform>().sizeDelta}");
            }
            sb.AppendLine("---------------------------");
        }

        Debug.Log(sb.ToString());
        Debug.Log("診断完了。Consoleログを確認してください。");
    }
}
