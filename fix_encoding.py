import re

path = r'Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, encoding='utf-8', errors='replace') as f:
    lines = f.readlines()

out = []
for i, line in enumerate(lines):
    ln = i + 1  # 1-indexed
    # Line 182: all-ready countdown log (broken Japanese)
    if ln == 182 and 'Debug.Log' in line and 'Ready' in line:
        out.append('                            Debug.Log($"[Ready] \u5168\u54e1Ready\u30005\u79d2\u5f8c\u306b\u30b2\u30fc\u30e0\u958b\u59cb\uff08\u73fe\u5728{activeCount}\u4eba\uff09");\n')
    # Line 318: host forced start log (broken Japanese)
    elif ln == 318 and 'Debug.Log' in line:
        out.append('                Debug.Log("[Ready] \u30db\u30b9\u30c8\u306b\u3088\u3063\u3066\u30b2\u30fc\u30e0\u304c\u5f37\u5236\u958b\u59cb\u3055\u308c\u307e\u3057\u305f\u3002");\n')
    else:
        out.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(out)

print(f'Done: fixed lines checked around 182 and 318')
