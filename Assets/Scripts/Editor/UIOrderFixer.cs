using UnityEngine;
using UnityEditor;

namespace DonGame2D.Editor
{
    public static class UIOrderFixer
    {
        [MenuItem("Tools/DonGame/Fix Spotlight Order")]
        public static void FixOrder()
        {
            GameObject spotlight = GameObject.Find("SpotlightEffect耳");
            if (spotlight != null && spotlight.transform.parent != null)
            {
                // 背景の次（インデックス1）に配置
                spotlight.transform.SetSiblingIndex(1);
                Debug.Log("[UI] SpotlightEffect sibling index set to 1.");
            }
        }
    }
}
