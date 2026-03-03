using UnityEditor;
using UnityEngine;

public static class TestReload
{
    [MenuItem("Tools/Test Reload Offset")]
    public static void Run()
    {
        string suitFolder = "Club";
        int offset = 0;
        switch (suitFolder)
        {
            case "Spade": offset = 0; break;
            case "Club": offset = 13; break;
            case "Diamond": offset = 26; break;
            case "Heart": offset = 39; break;
        }
        Debug.Log($"Test Reload: Club offset is {offset}");
    }
}
