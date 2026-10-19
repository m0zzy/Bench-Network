"""Temporary research helper: prints only member declarations (names and types, no code) of game types."""
import re
import subprocess
import sys

types = sys.argv[1].split(",")
dlls = sys.argv[2:]
decl = re.compile(r"^\s*((?:public|private|protected|internal)[^=;{]*?)(?:\s*=.*)?\s*[;{(]?\s*$")
for type_name in types:
    for dll in dlls:
        out = subprocess.run(["ilspycmd", "-t", type_name, dll], capture_output=True, text=True).stdout
        if re.search(r"\bclass\s+" + re.escape(type_name) + r"\b", out):
            print(f"== {type_name} ({dll.split('/')[-1]})")
            depth = 0
            for line in out.splitlines():
                stripped = line.strip()
                # Only lines at class-member depth (inside the class, outside any method body).
                if depth == 1 and re.match(r"(public|private|protected|internal)\b", stripped):
                    member = stripped.rstrip("{").rstrip()
                    if "(" not in member:
                        member = re.sub(r"\s*=.*$", "", member)  # drop field initial values
                    print("   " + member)
                depth += line.count("{") - line.count("}")
            break
    else:
        print(f"== {type_name}: not found")
