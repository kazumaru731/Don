using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace DonGame2D.Editor
{
    public static class DeepTypeScanner
    {
        [MenuItem("Tools/Fusion/Deep Scan Types")]
        public static void Scan()
        {
            string logPath = Path.Combine(Application.dataPath, "deep_scan_log.txt");
            File.WriteAllText(logPath, $"Deep Scan Started at {DateTime.Now}\n");
            
            Debug.Log($"Deep Scan Starting... Output: {logPath}");

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name.Contains("PhotonAppSettings"))
                        {
                            string entry = $"[MATCH] {type.FullName} | Assembly: {assembly.FullName}\n";
                            File.AppendAllText(logPath, entry);
                            Debug.Log(entry);
                        }
                    }
                } catch (Exception e) {
                    File.AppendAllText(logPath, $"[ERROR] {assembly.FullName}: {e.Message}\n");
                }
            }
            
            File.AppendAllText(logPath, "Deep Scan Complete.\n");
            Debug.Log("Deep Scan Complete.");
        }
    }
}
