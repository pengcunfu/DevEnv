# -*- coding: utf-8 -*-
"""
检查 src/data/software.json 中所有下载链接的可达性（HEAD 请求）。

用法:
    python scripts/check_links.py          # 全部检查
    python scripts/check_links.py -v       # 全部检查，含 200 明细

输出按状态分类：OK(200) / NOT_FOUND(404) / ERROR(网络或非 200)。
"""
import argparse
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "src" / "data" / "software.json"

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"
TIMEOUT = 20


def check(url: str):
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            return resp.status, resp.headers.get("Content-Length")
    except urllib.error.HTTPError as e:
        return e.code, None
    except Exception as e:
        return f"ERR:{type(e).__name__}", None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("-v", "--verbose", action="store_true", help="显示 200 明细")
    args = parser.parse_args()

    with open(DATA, "r", encoding="utf-8") as f:
        data = json.load(f)

    ok, not_found, errors = [], [], []
    total = 0
    for cat in data["categories"]:
        for sw in cat["softwares"]:
            for v in sw.get("versions", []):
                total += 1
                url = v.get("url", "")
                status, size = check(url)
                label = f"{cat['name']} / {sw['name']} / {v.get('version')} [{status}]"
                if status == 200:
                    ok.append((label, url))
                elif status in (404, 410):
                    not_found.append((label, url))
                else:
                    errors.append((label, url))
                print(f"  {label}  {url}")
                time.sleep(0.3)

    print(f"\n===== 汇总：共 {total} 个链接 =====")
    print(f"  OK(200):        {len(ok)}")
    print(f"  NOT_FOUND:      {len(not_found)}")
    print(f"  其他错误:        {len(errors)}")

    for title, group in (("失效(404/410)", not_found), ("其他错误", errors)):
        if group:
            print(f"\n--- {title} ---")
            for label, url in group:
                print(f"  {label}\n      {url}")

    if args.verbose:
        print("\n--- 200 明细 ---")
        for label, url in ok:
            print(f"  {label}\n      {url}")

    return 1 if (not_found or errors) else 0


if __name__ == "__main__":
    sys.exit(main())
