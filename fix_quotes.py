import re
path = r'Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, encoding='utf-8', errors='replace') as f:
    lines = f.readlines()

out = []
for i, line in enumerate(lines):
    if 'SwitchToGameUI()' in line and 'Debug.Log' in line:
        out.append('        Debug.Log("[FriendMatch] SwitchToGameUI() を直接呼び出し");\n')
    elif '[FriendMatch] ゲーム開始完' in line and 'Debug.Log' in line:
        out.append('    Debug.Log($"[FriendMatch] ゲーム開始完了: {targetPlayers}人構成");\n')
    else:
        out.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(out)

print("Done fixing quotes")
