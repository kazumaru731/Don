using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class HandLayoutFixer
{
    [MenuItem("Tools/Fix Hand Layout")]
    public static void Fix()
    {
        var allHlg = Object.FindObjectsOfType<HorizontalLayoutGroup>(true);
        foreach (var hlg in allHlg)
        {
            if (hlg.gameObject.name == "PlayerHandPanel")
            {
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = -30f;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                EditorUtility.SetDirty(hlg.gameObject);
                Debug.Log("PlayerHandPanel HLG settings fixed!");
            }
        }
    }
}
