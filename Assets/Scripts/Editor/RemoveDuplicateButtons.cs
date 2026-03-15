using UnityEngine;
using UnityEditor;

namespace DonGame2D.Editor
{
    public static class DuplicateButtonRemover
    {
        [MenuItem("Tools/DonGame/Remove Duplicate Back Buttons")]
        public static void RemoveDuplicates()
        {
            int removedCount = 0;
            // プレイモード・非プレイモード両対応のため GameObject.FindObjectsOfType を使用
            GameObject[] allButtons = GameObject.FindObjectsOfType<GameObject>(true);
            
            foreach (GameObject go in allButtons)
            {
                if (go.name == "CpuBackButton" || go.name == "BackButton")
                {
                    // カードと重なる y=-400 付近（ワールド座標またはローカル座標）のボタンを特定
                    // 以前の調査で 26776 は localPosition.y = -400 だった
                    if (Mathf.Abs(go.transform.localPosition.y + 400f) < 50f)
                    {
                        Debug.Log($"[Cleanup] Deleting duplicate button: {go.name} at {go.transform.localPosition}");
                        Object.DestroyImmediate(go);
                        removedCount++;
                    }
                }
            }
            
            Debug.Log($"[Cleanup] Removed {removedCount} duplicate back buttons.");
        }
    }
}
