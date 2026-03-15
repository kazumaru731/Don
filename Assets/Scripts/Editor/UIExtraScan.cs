
using UnityEngine;
using UnityEditor;
using DonGame2D.UI;
using System.Linq;

public class UIExtraScan
{
    [MenuItem("Tools/Scan Title UI Extra")]
    public static void Run()
    {
        var controller = Object.FindObjectOfType<TitleUIController>(true);
        if (controller == null) { Debug.LogError("Controller not found"); return; }
        
        Debug.Log("--- TITLE UI CONTROLLER ARRAY SCAN ---");
        System.Action<string, SelectionCard[]> logArr = (label, arr) => {
            if (arr == null) { Debug.Log($"{label}: null"); return; }
            Debug.Log($"{label}: {arr.Length} items -> " + string.Join(", ", arr.Select(c => c ? $"{c.name}({c.gameObject.GetInstanceID()})" : "null")));
        };
        logArr("Main", controller.mainSelectionCards);
        logArr("Host", controller.hostSelectionCards);
        logArr("CPU", controller.cpuSelectionCards);
        
        var container = controller.titleCanvasObj.transform.Find("SafeAreaContainer");
        if (container) {
            Debug.Log("--- SAFE AREA CONTAINER CHILDREN ---");
            foreach (Transform t in container) {
                var active = t.gameObject.activeSelf ? "ACTIVE" : "inactive";
                var sc = t.GetComponent<SelectionCard>();
                var hasSC = sc != null ? "SC" : "--";
                var hasBtn = t.GetComponent<UnityEngine.UI.Button>() != null ? "BTN" : "--";
                var rect = t.GetComponent<RectTransform>();
                var angles = t.localEulerAngles;
                Debug.Log($"[{active}] [{hasSC}] [{hasBtn}] {t.name}({t.gameObject.GetInstanceID()}) Pos:{rect.anchoredPosition} Size:{rect.sizeDelta} Rot:{angles.z}");
            }
        }
        
        var allSCs = Object.FindObjectsOfType<SelectionCard>(true);
        Debug.Log($"--- ALL SELECTION CARDS IN SCENE ({allSCs.Length}) ---");
        foreach(var c in allSCs) {
            Debug.Log($"{c.name}({c.gameObject.GetInstanceID()}) parent:{(c.transform.parent ? c.transform.parent.name : "null")} active:{c.gameObject.activeSelf}");
        }
    }
}
