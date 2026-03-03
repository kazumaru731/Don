import re
path = r'Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, encoding='utf-8', errors='replace') as f:
    lines = f.readlines()

out = []
for i, line in enumerate(lines):
    if 'Debug.Log' in line:
        # Check if the line has an odd number of unescaped quotes
        quotes = len(re.findall(r'(?<!\\)\"', line))
        if quotes % 2 != 0:
            # Replace the entire line with a safe log, keeping leading indentation
            indent = line[:len(line) - len(line.lstrip())]
            out.append(f'{indent}Debug.Log("[Log Recovered - encoding fix]");\n')
            continue
    out.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(out)

print("Done fixing all broken logs")
