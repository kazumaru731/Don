using UnityEngine;
using UnityEditor;
using System.Linq;
using DonGame2D.UI;

public class CleanupGhostDeckPile
{
    [MenuItem("Tools/Cleanup Ghost DeckPile")]
    public static void RunCleanup()
    {
        var controller = GameObject.FindObjectOfType<GameUIController>(true);
        if (controller == null)
        {
            Debug.LogError("GameUIController not found");
            return;
        }

        Transform validDeck = controller.deckPileContainer;
        
        // Find all objects named DeckPilePanel
        var allDecks = Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.name == "DeckPilePanel" && t.gameObject.hideFlags != HideFlags.NotEditable && t.gameObject.hideFlags != HideFlags.HideAndDontSave)
            .ToList();

        int removed = 0;
        foreach (var deck in allDecks)
        {
            // Make sure it's in the scene, not a prefab asset
            if (!string.IsNullOrEmpty(deck.gameObject.scene.name) && deck != validDeck)
            {
                Debug.Log($"Removing ghost DeckPilePanel: {deck.gameObject.name} (InstanceID: {deck.gameObject.GetInstanceID()})");
                GameObject.DestroyImmediate(deck.gameObject);
                removed++;
            }
        }

        if (removed > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"Successfully removed {removed} ghost DeckPilePanel(s).");
        }
        else
        {
            Debug.Log("No ghost DeckPilePanel found.");
        }
    }
}
