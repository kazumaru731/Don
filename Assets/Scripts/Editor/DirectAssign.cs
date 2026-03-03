using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DonGame2D.UI;

public class DirectAssign {
    public static void DoAssign() {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var uiCtrl = Object.FindObjectOfType<GameUIController>(true);
        if (uiCtrl != null) {
            var rects = Resources.FindObjectsOfTypeAll<RectTransform>();
            Transform target = null;
            foreach(var r in rects) {
                if (r.name == "DeckPilePanel" && r.gameObject.scene == scene) {
                    target = r;
                    break;
                }
            }
            if (target != null) {
                uiCtrl.deckPileContainer = target;
                EditorUtility.SetDirty(uiCtrl);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("SUCCESS: Assigned DeckPileContainer to GameUIController!");
            } else {
                Debug.LogError("ERROR: DeckPilePanel not found!");
            }
        } else {
            Debug.LogError("ERROR: GameUIController not found!");
        }
    }
}
