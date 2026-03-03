using UnityEngine;
using UnityEditor;

public class CheckCardSizes
{
    [MenuItem("Tools/DonGame/Check Card Sizes")]
    public static void Check()
    {
        CheckFile("Assets/Cards/A.png");
        CheckFile("Assets/Cards/Club/2-10.png");
        CheckFile("Assets/Cards/Club/J-Q-K.png");
    }

    private static void CheckFile(string path)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            Debug.Log($"{path}: {tex.width} x {tex.height}");
        }
        else
        {
            Debug.LogError($"Could not load {path}");
        }
    }
}
