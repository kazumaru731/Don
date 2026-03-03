using UnityEngine;
using UnityEditor;
using Fusion.Editor;
using Fusion.Photon.Realtime;

namespace DonGame2D.Editor
{
    /// <summary>
    /// Fusion 2 の App ID を設定する Editor ツール
    /// メニュー: Tools > Fusion > Apply AppId Alpha
    /// </summary>
    public static class FusionSetupAlpha
    {
        private const string APP_ID = "132479a5-77d5-4c8a-83b0-f0cc1700e5d3";

        [MenuItem("Tools/Fusion/Apply AppId Alpha")]
        public static void ApplyAppId()
        {
            Debug.Log("PhotonAppSettings.asset を確認・作成しています...");

            // アセットが存在しない場合は自動作成（Fusion.Editor 名前空間）
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();

            // アセットを取得
            if (!PhotonAppSettings.TryGetGlobal(out var settings))
            {
                Debug.LogError("PhotonAppSettings の取得に失敗しました。\n" +
                               "Fusion Hub（メニュー: Fusion > Fusion Hub）から一度開いてみてください。");
                return;
            }

            // App ID を設定
            settings.AppSettings.AppIdFusion = APP_ID;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=green>✓ App ID 設定完了: {APP_ID}</color>");

            // Project ウィンドウでアセットをハイライト
            EditorGUIUtility.PingObject(settings);
            Selection.activeObject = settings;
        }
    }
}
