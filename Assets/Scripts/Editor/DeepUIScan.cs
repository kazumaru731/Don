
using UnityEngine;
using UnityEditor;
using DonGame2D.UI;
using System.Linq;

public class DeepUIScan
{
    [MenuItem("Tools/Deep Scan UI Full")]
    public static void Run()
    {
        var controller = Object.FindObjectOfType<TitleUIController>(true);
        if (controller == null) { Debug.LogError("Controller not found"); return; }
        
        Debug.Log("--- CONTROLLER ARRAY DUMP ---");
        System.Action<string, SelectionCard[]> logArr = (label, arr) => {
            if (arr == null) { Debug.Log($"{label}: null"); return; }
            Debug.Log($"{label}: {arr.Length} items -> " + string.Join(", ", arr.Select(c => c ? $"{c.name}({c.gameObject.GetInstanceID()})" : "null")));
        };
        logArr("Main", controller.mainSelectionCards);
        logArr("Friend", controller.friendSelectionCards);
        logArr("Host", controller.hostSelectionCards);
        logArr("CPU", controller.cpuSelectionCards);

        Debug.Log("--- SAFE AREA CONTAINER CHILDREN ---");
        var container = controller.titleCanvasObj.transform.Find("SafeAreaContainer");
        if (container) {
            foreach (Transform t in container) {
                var sc = t.GetComponent<SelectionCard>();
                var rect = t.GetComponent<RectTransform>();
                Debug.Log($"[{(t.gameObject.activeSelf ? "ACTIVE" : "inactive")}] {(sc ? "[SC] " : "     ")}{t.name}({t.gameObject.GetInstanceID()}) Parent:{t.parent.name} Size:{rect.sizeDelta} Scale:{t.localScale} LossyScale:{t.lossyScale}");
            }
        }

        Debug.Log("--- ALL SELECTION CARDS SEARCH ---");
        var allSCs = Object.FindObjectsOfType<SelectionCard>(true);
        foreach (var sc in allSCs) {
            Debug.Log($"SC: {sc.name}({sc.gameObject.GetInstanceID()}) Active:{sc.gameObject.activeSelf} Parent:{(sc.transform.parent ? sc.transform.parent.name : "null")}");
        }

        var dropZone = Object.FindObjectOfType<SimpleDropZone>(true);
        if (dropZone) {
            Debug.Log($"DropZone: {dropZone.name} Active:{dropZone.gameObject.activeSelf} Parent:{dropZone.transform.parent.name}");
        } else {
            Debug.Log("DropZone: NOT FOUND");
        }
    }
}
