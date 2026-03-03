using UnityEngine;
using UnityEditor;

public class FindUITemp {
    public static void Find() {
        var rts = Object.FindObjectsOfType<RectTransform>(true);
        foreach (var rt in rts) {
            if (rt.gameObject.name.Contains("Discard")) {
                Debug.Log($"Found: {rt.gameObject.name} (Active: {rt.gameObject.activeInHierarchy})");
            }
        }
    }
}
