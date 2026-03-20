import sys
import os

def check_braces(filename):
    if not os.path.exists(filename):
        print(f"Error: File not found {filename}")
        return

    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()
    
    balance = 0
    lines = content.split('\n')
    for i, line in enumerate(lines):
        line_num = i + 1
        # Skip strings and comments for more robust check
        clean_line = ""
        in_string = False
        in_char = False
        skip_next = False
        for j, char in enumerate(line):
            if skip_next:
                skip_next = False
                continue
            if char == '\\' and (in_string or in_char):
                skip_next = True
                continue
            if char == '"' and not in_char:
                in_string = not in_string
            if char == "'" and not in_string:
                in_char = not in_char
            
            if not in_string and not in_char:
                if line[j:j+2] == '//':
                    break # Comment starts
                clean_line += char
        
        for char in clean_line:
            if char == '{':
                balance += 1
            elif char == '}':
                balance -= 1
                if balance < 0:
                    print(f"Error: Extra closing brace at line {line_num}: {line.strip()}")
                    # continue to find more
        # if line_num % 100 == 0:
        #     print(f"Line {line_num}: balance {balance}")
    
    print(f"Final balance: {balance}")
    if balance > 0:
        print(f"Error: {balance} missing closing braces at end of file")
    elif balance < 0:
        print(f"Error: Overall extra closing braces (Final Balance: {balance})")
    else:
        print("Braces are perfectly balanced!")

if __name__ == "__main__":
    check_braces(r'd:\Unity_projects\Don\Assets\Scripts\UI\GameUIController.cs')
