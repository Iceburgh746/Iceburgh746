from pathlib import Path
import re, sys
root = Path(__file__).resolve().parents[1] / 'src'
errors=[]
for p in sorted(root.glob('*.cs')):
    s=p.read_text(encoding='utf-8')
    checks=[
        (r'\{\s*get;\s*set;\s*\}\s*=', 'auto-property initializer (C# 6+)'),
        (r'\$"', 'interpolated string (C# 6+)'),
        (r'\?\.', 'null-conditional operator (C# 6+)'),
        (r'\bnameof\s*\(', 'nameof operator (C# 6+)'),
        (r'\busing\s+static\b', 'using static (C# 6+)'),
        (r'\bout\s+var\b', 'out var (C# 7+)'),
    ]
    for pat,desc in checks:
        for m in re.finditer(pat,s):
            line=s.count('\n',0,m.start())+1
            errors.append(f'{p.name}:{line}: {desc}')

    # Catch the v0.9.10 property/type name collision in the custom chrome.
    for m in re.finditer(r'\bWindowState\s*=\s*WindowState\.', s):
        line=s.count('\n',0,m.start())+1
        errors.append(f'{p.name}:{line}: WindowState property used as if it were the FormWindowState enum')

    # Detect the accidental multiline-string pattern that broke v0.9.9.
    # This codebase does not intentionally use verbatim multiline strings.
    lines=s.splitlines()
    for n,l in enumerate(lines,1):
        # Remove escaped quotes before counting ordinary quote delimiters.
        q=re.sub(r'\\"', '', l)
        # Character literals containing a double quote are not string delimiters.
        q=q.replace("'\"'", "")
        if q.count('"') % 2:
            errors.append(f'{p.name}:{n}: unmatched quote / possible multiline ordinary string')
if errors:
    print('\n'.join(errors)); sys.exit(1)
print('ALL C#5 COMPATIBILITY CHECKS PASSED')
