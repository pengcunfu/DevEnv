import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
VIEWS = ROOT / "Views"

ENCODINGS = ("utf-8", "utf-8-sig", "gbk", "gb2312", "cp936", "latin-1")


def read_text(path: pathlib.Path) -> tuple[str, str]:
    data = path.read_bytes()
    for enc in ENCODINGS:
        try:
            return data.decode(enc), enc
        except UnicodeDecodeError:
            continue
    return data.decode("utf-8", errors="replace"), "utf-8-replace"


def fix_mojibake(text: str) -> str:
    try:
        repaired = text.encode("latin-1").decode("utf-8")
        if repaired != text and not repaired.count("?"):
            return repaired
    except (UnicodeEncodeError, UnicodeDecodeError):
        pass
    return text


def main() -> None:
    for path in sorted(VIEWS.glob("*.axaml")):
        text, enc = read_text(path)
        original = text
        if "??" in text:
            print(f"{path.name}: contains literal ?? (source encoding: {enc})")
        if enc not in ("utf-8", "utf-8-sig"):
            print(f"{path.name}: re-encoded from {enc} -> utf-8")
            path.write_text(text, encoding="utf-8", newline="\n")
            continue
        repaired = fix_mojibake(text)
        if repaired != text:
            print(f"{path.name}: repaired latin1 mojibake")
            path.write_text(repaired, encoding="utf-8", newline="\n")
            continue
        if text != original:
            path.write_text(text, encoding="utf-8", newline="\n")

    print("done")


if __name__ == "__main__":
    main()
