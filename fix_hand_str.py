import re

path = r'd:\Unity_projects\Don\Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, 'rb') as f:
    content = f.read()

# Replace all occurrences of the old handStr generation line with the new one
old_line = b'string handStr = string.Join(",", hand.Select(c => c.ToString()));'
new_line = b'string handStr = string.Join(";", hand.Select(c => $"{(int)c.Suit},{c.Rank}"));'

if old_line in content:
    new_content = content.replace(old_line, new_line)
    with open(path, 'wb') as f:
        f.write(new_content)
    print("Success: Replaced all occurrences.")
else:
    print("Error: Target line not found in binary content.")
