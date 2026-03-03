using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DonGame2D.UI;

public class AssignDeckContainerTEMP {
    [MenuItem("Tools/DonGame/Assign Deck Container")]
    public static void Assign() {
        GameUIController uiCtrl = Object.FindObjectOfType<GameUIController>(true);
        if (uiCtrl == null) {
            Debug.LogError("GameUIController not found in scene!");
            return;
        }

        GameObject deckPile = GameObject.Find("DeckPilePanel");
        if (deckPile == null) {
            Debug.LogError("DeckPilePanel not found! Did you run 'Add Deck UI' command?");
            return;
        }

        Undo.RecordObject(uiCtrl, "Assign DeckPileContainer");
        uiCtrl.deckPileContainer = deckPile.transform;
        
        EditorUtility.SetDirty(uiCtrl);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("Successfully assigned DeckPilePanel to GameUIController.deckPileContainer!");
    }
}
