using UnityEngine;
using UnityEditor;

public class InspectCardDatabase
{
    [MenuItem("Tools/DonGame/Inspect Debug")]
    public static void Inspect()
    {
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath("Assets/Cards/A.png");
        Debug.Log($"[Inspect Debug] Found {assets.Length} sub-assets in A.png");
        foreach(var a in assets)
        {
            Debug.Log($"  Type: {a.GetType().Name}, Name: {a.name}");
        }
        
        string path = "Assets/Cards/Club/Club_J-Q-K.png";
        var assets2 = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        Debug.Log($"[Inspect Debug] Found {assets2.Length} sub-assets in {path}");
    }
}
