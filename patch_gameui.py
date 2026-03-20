import sys
import os

def fix_file(filename):
    with open(filename, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    start_line = -1
    end_line = -1
    
    for i, line in enumerate(lines):
        if 'private void UpdateFusionUIState()' in line:
            start_line = i
            break
            
    if start_line == -1:
        print("Could not find start of UpdateFusionUIState")
        return

    # Find the matching closing brace for the method
    balance = 0
    found_start = False
    for i in range(start_line, len(lines)):
        line = lines[i]
        for char in line:
            if char == '{':
                balance += 1
                found_start = True
            elif char == '}':
                balance -= 1
                if found_start and balance == 0:
                    end_line = i
                    break
        if end_line != -1:
            break
            
    if end_line == -1:
        print("Could not find end of UpdateFusionUIState")
        return
        
    print(f"Replacing lines {start_line+1} to {end_line+1}")
    
    new_method = """        private void UpdateFusionUIState()
        {
            var fm = DonFusionManager2D.Instance;
            if (fm == null) return;

            if (!isDonButtonSetup)
            {
                if (discardDonButton == null)
                {
                    CreateContextDonButton();
                }

                if (discardDonButton != null)
                {
                    discardDonButton.onClick.RemoveAllListeners();
                    discardDonButton.onClick.AddListener(() =>
                    {
                        if (useFusion && DonFusionManager2D.Instance != null && DonFusionManager2D.Instance.Object != null)
                        {
                            int localId = GetLocalActorId();
                            if (DonFusionManager2D.Instance.IsWaitingForDonGaeshi && DonFusionManager2D.Instance.DonTargetActorId == localId)
                            {
                                DonFusionManager2D.Instance.RPC_DeclareDonGaeshi(DonFusionManager2D.Instance.Runner.LocalPlayer);
                            }
                            else if (DonFusionManager2D.Instance.IsDonWindowOpen)
                            {
                                DonFusionManager2D.Instance.RPC_DeclareDon(DonFusionManager2D.Instance.Runner.LocalPlayer);
                            }
                        }
                    });
                    discardDonButton.gameObject.SetActive(false);
                    isDonButtonSetup = true;
                    Debug.Log($"[Don] Donボタンの初期化完了: {discardDonButton.gameObject.name}");
                }
            }

            int localActorId = GetLocalActorId();
            bool isMyTurn = (fm.Runner != null && fm.CurrentTurnPlayerActorId == localActorId);

            if (notificationTimer > 0)
            {
                statusText.text = temporaryNotification;
                statusText.color = Color.yellow;
                notificationTimer -= Time.deltaTime;
            }
            else
            {
                statusText.color = Color.white;
                if (fm.IsWaitingForDonGaeshi)
                {
                    if (fm.DonTargetActorId == localActorId)
                    {
                        statusText.text = "Don-Gaeshi Chance!";
                    }
                    else
                    {
                        statusText.text = "Waiting for Don-Gaeshi...";
                    }
                }
                else if (fm.RoundEndTimer.IsRunning)
                {
                    statusText.text = "Starting Next Round... (" + Mathf.CeilToInt(fm.RoundEndTimer.RemainingTime(fm.Runner) ?? 0f) + "s)";
                }
                else
                {
                    statusText.text = isMyTurn ? "Your Turn" : "Opponent's Turn";
                }
            }

            if (fm.PlayerCredits.TryGet(localActorId, out var credits)) { } else credits = 0;
            
            if (roundText != null)
            {
                roundText.text = $"{fm.CurrentRound}/5";
                if (roundText.transform.parent != null && !roundText.transform.parent.gameObject.activeSelf)
                {
                    roundText.transform.parent.gameObject.SetActive(true);
                }

                string scoreInfo = $"{credits} Credits";
                if (fm.DrawPenaltyCount > 0) scoreInfo += $" | Penalty: +{fm.DrawPenaltyCount}";
                penaltyText.text = scoreInfo;
            }
            else
            {
                string scoreInfo = $"RD {fm.CurrentRound}/5 | {credits} Credits";
                if (fm.DrawPenaltyCount > 0) scoreInfo += $" | Penalty: +{fm.DrawPenaltyCount}";
                penaltyText.text = scoreInfo;
            }

            if (fm.IsRoundOver && resultPanel != null && resultPanel.activeSelf)
            {
                var btn = resultPanel.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    var btnText = btn.GetComponentInChildren<Text>();
                    if (btnText != null && fm.RoundEndTimer.IsRunning)
                    {
                        int remain = Mathf.CeilToInt(fm.RoundEndTimer.RemainingTime(fm.Runner) ?? 0f);
                        btnText.text = fm.CurrentRound >= 5 ? "GAME OVER" : $"NEXT ROUND ({remain}s)";
                    }
                }
            }

            if (discardDonButton != null)
            {
                bool canDon = false;
                if (!fm.IsRoundOver && fm.DiscardCount > 0 && localActorId != -1)
                {
                    var topCard = fm.DiscardPile.Get(fm.DiscardCount - 1);
                    int myTotal = 0;
                    foreach (var c in fm.myLocalHand) myTotal += c.Rank;

                    if (fm.IsWaitingForDonGaeshi)
                    {
                        if (fm.DonTargetActorId == localActorId && myTotal == topCard.Rank) canDon = true;
                    }
                    else
                    {
                        if (fm.LastPlayedPlayerActorId != localActorId && myTotal == topCard.Rank && myTotal <= 13) canDon = true;
                    }
                }

                if (canDon)
                {
                    var rt = discardDonButton.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        if (playerHandContainer != null && rt.parent != playerHandContainer.parent)
                        {
                            rt.SetParent(playerHandContainer.parent, true);
                        }
                        
                        rt.anchorMin = new Vector2(0.5f, 0f);
                        rt.anchorMax = new Vector2(0.5f, 0f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = new Vector2(0, 460f);
                        rt.sizeDelta = new Vector2(600, 200);
                        rt.SetAsLastSibling();
                        
                        var txt = discardDonButton.GetComponentInChildren<Text>();
                        if (txt != null)
                        {
                            txt.fontSize = 80;
                            txt.fontStyle = FontStyle.Bold;
                            txt.color = Color.white;
                            txt.text = "DON!";
                        }

                        var img = discardDonButton.GetComponent<Image>();
                        if (img != null)
                        {
                            img.color = new Color(1f, 0.84f, 0f, 1f);
                        }
                    }
                }

                discardDonButton.gameObject.SetActive(canDon);
                discardDonButton.interactable = canDon && !IsInteractionBlocked;
            }

            UpdateFusionDiscardPileUI();

            if (fm.IsRoundOver)
            {
                resultPanel.SetActive(true);
                string winnerName = (fm.WinnerActorId != -1) ? $"Player {fm.WinnerActorId}" : "Someone";
                resultText.text = $"{winnerName} Wins!";
            }
            else
            {
                resultPanel.SetActive(false);
                if (revealedHandContainer != null)
                {
                    foreach (Transform child in revealedHandContainer)
                        Destroy(child.gameObject);
                }
            }
            UpdateOpponentsUI();
        }
"""
    
    lines[start_line:end_line+1] = [new_method + "\n"]
    
    with open(filename, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    print("Successfully patched file.")

if __name__ == "__main__":
    fix_file(r"d:\Unity_projects\Don\Assets\Scripts\UI\GameUIController.cs")
