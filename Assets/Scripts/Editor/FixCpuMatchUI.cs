using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DonGame2D.UI;
using System.Collections.Generic;
using System.Linq;

namespace DonGame2D.Editor
{
    public class FixCpuMatchUI : EditorWindow
    {
        [MenuItem("Tools/Don/Fix CPU Match UI")]
        public static void FixUI()
        {
            var controller = Object.FindObjectOfType<TitleUIController>(true);
            if (controller == null)
            {
                Debug.LogError("TitleUIController not found.");
                return;
            }

            // 1. Assign New CPU Buttons from RoundSettingPanel
            if (controller.roundSettingPanel == null) {
                var panels = Object.FindObjectsOfType<GameObject>(true).Where(go => go.name == "RoundSettingPanel").ToList();
                if (panels.Any()) controller.roundSettingPanel = panels.First();
            }

            if (controller.roundSettingPanel != null) {
                var cpuGroup = controller.roundSettingPanel.transform.Find("CpuGroup");
                if (cpuGroup != null) {
                    controller.cpuAddButton = cpuGroup.Find("CpuAddButton")?.GetComponent<Button>();
                    controller.cpuRemoveButton = cpuGroup.Find("CpuRemoveButton")?.GetComponent<Button>();
                    controller.cpuCountLabel = cpuGroup.Find("CpuCountLabel")?.GetComponent<Text>();
                    Debug.Log("Assigned new CPU buttons to TitleUIController fields.");
                }
            }

            // 2. Card Management in CPU Match Panel
            var cpuPanel = controller.cpuMatchPanel;
            if (cpuPanel != null) {
                // Deactivate Card2,3,4
                var cards = cpuPanel.GetComponentsInChildren<SelectionCard>(true);
                foreach (var card in cards) {
                    if (card.name == "Card2" || card.name == "Card3" || card.name == "Card4")
                        card.gameObject.SetActive(false);
                }
            }

            // 3. Find and Assign Cards for CPU Match Selection
            SelectionCard cpuStartCard = null;
            if (cpuPanel != null) {
                cpuStartCard = cpuPanel.GetComponentsInChildren<SelectionCard>(true).FirstOrDefault(c => c.name == "CpuStartCard");
            }
            if (cpuStartCard == null) {
                 var template = Object.FindObjectsOfType<SelectionCard>(true).FirstOrDefault(c => c.name == "HostStartCard");
                 if (template != null) {
                     var go = (GameObject)Object.Instantiate(template.gameObject);
                     go.name = "CpuStartCard";
                     go.transform.SetParent(cpuPanel.transform, false);
                     cpuStartCard = go.GetComponent<SelectionCard>();
                 }
            }

            SelectionCard cancelCard = Object.FindObjectsOfType<SelectionCard>(true).FirstOrDefault(c => c.name.Contains("Cansel") || c.name.Contains("Cancel"));
            
            var cardList = new List<SelectionCard>();
            if (cpuStartCard != null) cardList.Add(cpuStartCard);
            if (cancelCard != null) cardList.Add(cancelCard);
            
            controller.cpuSelectionCards = cardList.ToArray();

            // 4. Final Save
            EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log($"CPU Match fixed: Cards={cardList.Count}, Buttons assigned.");
        }



    }
}
