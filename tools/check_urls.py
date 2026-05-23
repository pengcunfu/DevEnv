import json
import ssl
import urllib.error
import urllib.request
from pathlib import Path

ctx = ssl.create_default_context()
data = json.loads(
    Path(r"D:\Projects\DevTools\DevEnv\devenv\Resources\software_config.json").read_text(
        encoding="utf-8"
    )
)

for cat, items in data.items():
    for item in items:
        for v in item["versions"]:
            url = v["url"]
            req = urllib.request.Request(
                url, method="HEAD", headers={"User-Agent": "DevEnv/1.0"}
            )
            try:
                with urllib.request.urlopen(req, context=ctx, timeout=20) as r:
                    print(f"OK {r.status} {item['name']} {v['version']}")
            except urllib.error.HTTPError as e:
                print(f"FAIL {e.code} {item['name']} {v['version']} {url}")
            except Exception as e:
                print(f"ERR {item['name']} {v['version']} {e} {url}")
