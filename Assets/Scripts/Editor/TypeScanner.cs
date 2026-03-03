using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System;

namespace DonGame2D.Editor
{
    public static class TypeScanner
    {
        [MenuItem("Tools/Fusion/Scan Types")]
        public static void Scan()
        {
            string resultPath = Path.Combine(Application.dataPath, "type_scan_results.txt");
            try {
                File.WriteAllText(resultPath, "Starting detailed scan...\n");
                Debug.Log($"Scanning assemblies for PhotonAppSettings. Output to: {resultPath}");

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try {
                        var types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            if (type.Name.Contains("PhotonAppSettings"))
                            {
                                string log = $"Found: {type.FullName}, Assembly: {assembly.FullName}\n";
                                File.AppendAllText(resultPath, log);
                                Debug.Log(log);
                            }
                        }
                    } catch (Exception ex) {
                        File.AppendAllText(resultPath, $"Error scanning assembly {assembly.FullName}: {ex.Message}\n");
                    }
                }
                File.AppendAllText(resultPath, "Scan complete.\n");
                Debug.Log("Scan complete.");
            } catch (Exception fatalEx) {
                Debug.LogError($"Fatal error in TypeScanner: {fatalEx.Message}");
            }
        }
    }
}
