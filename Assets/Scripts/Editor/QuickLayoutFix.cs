using UnityEngine;
using UnityEditor;
using DonGame2D.UI;

namespace DonGame2D.Editor
{
    public class QuickLayoutFix : EditorWindow
    {
        [MenuItem("DonGame/Quick Layout Fix")]
        public static void Execute()
        {
            FixCanvas("GameCanvas");
            FixCanvas("TitleCanvas");
            Debug.Log("Quick Layout Fix completed!");
        }

        private static void FixCanvas(string canvasName)
        {
            GameObject canvasObj = GameObject.Find(canvasName);
            if (canvasObj == null) 
            {
                foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                {
                    if (c.name == canvasName)
                    {
                        canvasObj = c.gameObject;
                        break;
                    }
                }
            }

            if (canvasObj == null) return;

            Undo.RegisterFullObjectHierarchyUndo(canvasObj, "Quick Layout Fix");

            // レスポンシブ対応 (CanvasScaler)
            var scaler = canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            Transform bg = FindBackground(canvasObj.transform);
            if (bg != null)
            {
                bg.SetParent(canvasObj.transform);
                bg.SetAsFirstSibling();
                RectTransform rt = bg.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.localScale = Vector3.one;
                    rt.anchoredPosition = Vector2.zero;
                }
            }

            Transform container = canvasObj.transform.Find("SafeAreaContainer") ?? 
                                canvasObj.transform.Find("SafeAreaContainer_Game");
            
            if (container == null)
            {
                GameObject newContainer = new GameObject("SafeAreaContainer", typeof(RectTransform));
                newContainer.transform.SetParent(canvasObj.transform);
                container = newContainer.transform;
            }

            RectTransform crt = container.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            crt.localScale = Vector3.one;
            crt.anchoredPosition3D = Vector3.zero;

            System.Type safeAreaType = System.Type.GetType("DonGame2D.UI.SafeArea, Assembly-CSharp");
            if (safeAreaType != null && container.GetComponent(safeAreaType) == null) 
                container.gameObject.AddComponent(safeAreaType);

            System.Collections.Generic.List<Transform> children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in canvasObj.transform)
            {
                if (child != bg && child != container) children.Add(child);
            }

            foreach (var child in children)
            {
                child.SetParent(container, true);
                child.localScale = Vector3.one;
            }

            if (canvasName == "GameCanvas")
            {
                Transform hand = container.Find("PlayerHandPanel") ?? container.Find("PlayerHand");
                if (hand != null)
                {
                    RectTransform rt = hand.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(0f, 350f);
                    rt.anchoredPosition = new Vector2(0f, 50f);
                    rt.localScale = Vector3.one;

                    var hlg = hand.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    if (hlg != null)
                    {
                        hlg.childAlignment = TextAnchor.LowerCenter;
                        hlg.spacing = -40f;
                        hlg.childControlWidth = true;
                        hlg.childControlHeight = true;
                        hlg.childForceExpandWidth = false;
                        hlg.childForceExpandHeight = false;
                    }
                }

                Transform oppInfo = container.Find("OpponentInfoContainer") ?? container.Find("OpponentInfoPanel");
                if (oppInfo != null)
                {
                    RectTransform rt = oppInfo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 1f); 
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -120f); // ノッチを避けるためにさらに下げる (-50 -> -120)
                    rt.localScale = new Vector3(1.2f, 1.2f, 1f);
                }

                Transform donBtn = container.Find("DonButton");
                if (donBtn != null) FixMainButton(donBtn, new Vector2(0f, 0f), new Vector2(50f, 400f));

                Transform drawBtn = container.Find("DrawButton");
                if (drawBtn != null) FixMainButton(drawBtn, new Vector2(1f, 0f), new Vector2(-50f, 400f));

                // 【位置の最終調整】: 捨て札を左(0.35), 山札を右(0.65) に再固定
                Transform disc = container.Find("DiscardPilePanel") ?? container.Find("DiscardPile");
                if (disc != null) {
                    disc.name = "DiscardPilePanel";
                    FixPilePanel(disc, new Vector2(0.35f, 0.5f));
                }

                Transform deck = container.Find("DeckPilePanel") ?? container.Find("DeckPile");
                if (deck != null) {
                    deck.name = "DeckPilePanel";
                    FixPilePanel(deck, new Vector2(0.65f, 0.5f));
                }

                // 枠(Frame)などの装飾オブジェクトを個別に補正
                foreach (Transform child in container)
                {
                    string n = child.name.ToLower();
                    // 捨て札(左)に関連する装飾
                    if (n.Contains("discard") && (n.Contains("frame") || n.Contains("border") || n.Contains("slot") || n.Contains("outline")))
                    {
                        FixPilePanel(child, new Vector2(0.35f, 0.5f));
                    }
                    // 山札(右)に関連する装飾
                    else if (n.Contains("deck") && (n.Contains("frame") || n.Contains("border") || n.Contains("slot") || n.Contains("outline")))
                    {
                        FixPilePanel(child, new Vector2(0.65f, 0.5f));
                    }
                    
                    // 【重要】全画面を覆う可能性のあるパーツの Raycast を徹底オフ
                    if (n.Contains("panel") || n.Contains("container") || n.Contains("background") || n.Contains("overlay")) {
                        if (n != "discardpilepanel" && n != "deckpilepanel") {
                            var img = child.GetComponent<UnityEngine.UI.Image>();
                            if (img != null) img.raycastTarget = false;
                        }
                    }
                }

                // クリック遮断防止
                DisableRaycastsRecursive(deck);
                DisableRaycastsRecursive(disc);
            }

            if (canvasName == "TitleCanvas")
            {
                foreach (Transform child in container)
                {
                    if (child.name.Contains("Background")) continue;

                    float yOffset = 0f;
                    bool isTopLevel = true;

                    if (child.name == "GameTitle" || child.name == "TitleText") yOffset = 550f;
                    else if (child.name.Contains("PlayersCountText") || child.name.Contains("StatusText")) yOffset = 380f;
                    else if (child.name == "RandomMatchButton") yOffset = 150f;
                    else if (child.name == "FriendMatchButton") yOffset = 0f;
                    else if (child.name == "CpuMatchButton") yOffset = -150f;
                    else if (child.name == "ReadyButton" || child.name == "HostStartButton" || child.name == "RandomMatchBackButton" || child.name.Contains("Cancel"))
                        yOffset = -350f;
                    else if (child.name.Contains("Panel") || child.name.Contains("Container"))
                    {
                        yOffset = 0f;
                        isTopLevel = false;
                        FixTitleElementFinal(child, true, yOffset);
                        continue;
                    }

                    FixTitleElementFinal(child, isTopLevel, yOffset);
                }
            }
        }

        private static void FixTitleElementFinal(Transform t, bool isTopLevel, float yOffset)
        {
            if (t == null) return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt == null) return;

            if (isTopLevel)
            {
                Vector2 size = rt.rect.size;
                if (size.x <= 10) size = new Vector2(400, 100); 

                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size;
                rt.anchoredPosition = new Vector2(0, yOffset);
                rt.localScale = new Vector3(2f, 2f, 1f);

                if (t.name.Contains("Panel") || t.name.Contains("Container")) 
                {
                    rt.sizeDelta = new Vector2(500f, 600f);
                    float innerY = 100f;
                    foreach (Transform child in t) 
                    {
                        RectTransform crt = child.GetComponent<RectTransform>();
                        if (crt == null || !child.gameObject.activeSelf) continue;
                        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                        crt.sizeDelta = new Vector2(child.name.Contains("Text") ? 450f : 350f, 80f);
                        crt.anchoredPosition = new Vector2(0, innerY);
                        crt.localScale = Vector3.one;
                        innerY -= 110f;
                        foreach(Transform grandChild in child) ResetInternalUI(grandChild, false);
                    }
                }  
                else 
                {
                    foreach (Transform child in t) ResetInternalUI(child, false);
                }
            }
        }

        private static void ResetInternalUI(Transform t, bool recursive)
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            if (recursive)
            {
                foreach (Transform child in t) ResetInternalUI(child, true);
            }
        }

        private static void FixMainButton(Transform t, Vector2 anchor, Vector2 offset)
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(180, 180);
            rt.localScale = Vector3.one;
            foreach (Transform child in t) ResetInternalUI(child, false);
        }

        private static void FixPilePanel(Transform t, Vector2 anchor)
        {
            if (t == null) return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            // カード本体（DiscardPileCard）以外は、カード(150x210)より一回り大きいサイズ(170x230)にする
            if (t.name == "DiscardPileCard" || t.GetComponent<CardUI>() != null)
                rt.sizeDelta = new Vector2(150f, 210f);
            else
                rt.sizeDelta = new Vector2(170f, 230f);
            
            // 【重要】枠(Border)はクリックを遮断しないよう raycastTarget を必ず false にする
            string nameLow = t.name.ToLower();
            if (nameLow.Contains("border") || nameLow.Contains("frame") || nameLow.Contains("outline"))
            {
                var img = t.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = false;
                // 子要素も全てオフ
                foreach (Transform child in t)
                {
                    var childImg = child.GetComponent<UnityEngine.UI.Image>();
                    if (childImg != null) childImg.raycastTarget = false;
                }
            }
            
            // 子要素（黄色の枠、背景画像、テキスト等）があればそれらも親いっぱいに広げる
            foreach (Transform child in t) {
                ResetInternalUI(child, true);
            }
        }

        private static void DisableRaycastsRecursive(Transform t)
        {
            if (t == null) return;
            var img = t.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                string n = t.name;
                // DeckPilePanel 本体だけ raycastTarget を維持（ボタンとして機能させる）
                // それ以外の全ての子要素（枠・背景・装飾含む）は必ず false にする
                if (n != "DeckPilePanel" && n != "DiscardPilePanel")
                    img.raycastTarget = false;
            }
            
            foreach (Transform child in t) DisableRaycastsRecursive(child);
        }

        private static Transform FindBackground(Transform parent)
        {
            if (parent.name.Contains("Background")) return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindBackground(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
