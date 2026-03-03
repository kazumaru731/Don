using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Text;

public class SceneStateDumper
{
    [MenuItem("Tools/Debug/Dump Hand Layout State")]
    public static void Dump()
    {
        var panel = GameObject.Find("PlayerHandPanel");
        if (panel == null)
        {
            Debug.LogError("PlayerHandPanel not found!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Panel: {panel.name} ---");
        var rt = panel.GetComponent<RectTransform>();
        sb.AppendLine($"SizeDelta: {rt.sizeDelta}, Anchor: {rt.anchorMin}-{rt.anchorMax}, Pivot: {rt.pivot}");

        var hlg = panel.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            sb.AppendLine($"HLG: spacing={hlg.spacing}, align={hlg.childAlignment}");
            sb.AppendLine($"HLG Controls: width={hlg.childControlWidth}, height={hlg.childControlHeight}");
            sb.AppendLine($"HLG Expand: width={hlg.childForceExpandWidth}, height={hlg.childForceExpandHeight}");
        }

        sb.AppendLine("\n--- Children ---");
        for (int i = 0; i < panel.transform.childCount; i++)
        {
            var child = panel.transform.GetChild(i);
            var childRt = child.GetComponent<RectTransform>();
            var layout = child.GetComponent<LayoutElement>();
            sb.AppendLine($"[{i}] {child.name}: size={childRt.sizeDelta}, scale={child.localScale}");
            if (layout != null)
            {
                sb.AppendLine($"    LayoutElement: pref={layout.preferredWidth}x{layout.preferredHeight}, min={layout.minWidth}x{layout.minHeight}");
            }
            var img = child.GetComponentInChildren<Image>();
            if (img != null)
            {
                sb.AppendLine($"    Image: preserveAspect={img.preserveAspect}, sprite={img.sprite?.name}, spriteRect={img.sprite?.rect}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
