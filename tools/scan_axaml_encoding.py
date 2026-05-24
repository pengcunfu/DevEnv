import re
import pathlib

root = pathlib.Path(__file__).resolve().parents[1] / "Views"
for p in sorted(root.glob("*.axaml")):
    text = p.read_text(encoding="utf-8", errors="replace")
    q = text.count("??")
    moj = len(re.findall(r"[À-ÿ]{2,}", text))
    bad = "????" in text or q > 3 or moj > 2
    print(f"{p.name}: qmarks={q}, mojibake={moj}, bad={bad}")
