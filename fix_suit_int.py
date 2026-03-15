path = r'd:\Unity_projects\Don\Assets\Scripts\Logic\DonFusionManager2D.cs'
with open(path, 'rb') as f:
    content = f.read()

# Replace all occurrences of the casting-based serialize with direct SuitInt
old_line = b'string.Join(";", hand.Select(c => $"{(int)c.Suit},{c.Rank}"))'
new_line = b'string.Join(";", hand.Select(c => $"{c.SuitInt},{c.Rank}"))'

if old_line in content:
    new_content = content.replace(old_line, new_line)
    with open(path, 'wb') as f:
        f.write(new_content)
    print("Success: Replaced all occurrences in DonFusionManager2D.cs.")
else:
    print("Error: Target pattern not found.")
