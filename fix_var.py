import re
path = r'Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, encoding='utf-8', errors='replace') as f:
    lines = f.readlines()

out = []
for line in lines:
    if 'var titleUI =' in line and '// 3)' in line:
        # Split it and insert a newline
        idx = line.find('var titleUI')
        part1 = line[:idx]
        part2 = "    " + line[idx:]
        out.append(part1 + '\n')
        out.append(part2)
    else:
        out.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(out)

print("Done fixing var declaration")
