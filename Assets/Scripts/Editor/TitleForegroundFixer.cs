using UnityEngine;
using UnityEditor;

namespace DonGame2D.Editor
{
    public static class TitleForegroundFixer
    {
        [MenuItem("Tools/DonGame/Make Title Topmost")]
        public static void Fix()
        {
            GameObject titleObj = GameObject.Find("GameTitle");
            GameObject canvasObj = GameObject.Find("TitleCanvas");
            
            if (titleObj == null || canvasObj == null)
            {
                Debug.LogError("Required objects not found!");
                return;
            }

            // 1. TitleCanvas 直下へ移動 (SafeAreaContainer から出す)
            // ただし、位置を維持するためにワールド座標を保持
            Vector3 worldPos = titleObj.transform.position;
            Quaternion worldRot = titleObj.transform.rotation;
            
            titleObj.transform.SetParent(canvasObj.transform, true);
            
            // 2. 階層の最後に移動 (＝最前面へ)
            titleObj.transform.SetAsLastSibling();

            // 3. 他のテキスト（PlayersCountTextなど）も必要か？
            // ユーザーは「タイトル」と言及しているので、まずは GameTitle のみ対応
            
            Debug.Log("[UI] GameTitle moved to TitleCanvas root and set as last sibling (Topmost).");
        }
    }
}
