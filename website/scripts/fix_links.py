# -*- coding: utf-8 -*-
"""
一次性修复 scripts/check_links.py 检出的失效下载链接（2026-09 实测）。

用法: python scripts/fix_links.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "src" / "data" / "software.json"


def find(data, cat_name, sw_name):
    for cat in data["categories"]:
        if cat["name"] == cat_name:
            for sw in cat["softwares"]:
                if sw["name"] == sw_name:
                    return sw
    raise KeyError(f"{cat_name}/{sw_name}")


def main():
    data = json.loads(DATA.read_text(encoding="utf-8"))

    # --- Java: 清华镜像当前可用版本（已验证 200）---
    jdk = find(data, "Java", "OpenJDK")
    jdk_new = {
        "8": "OpenJDK8U-jdk_x64_windows_hotspot_8u504b01.zip",
        "11": "OpenJDK11U-jdk_x64_windows_hotspot_11.0.32.1_1.zip",
        "17": "OpenJDK17U-jdk_x64_windows_hotspot_17.0.20.1_1.zip",
    }
    for v in jdk["versions"]:
        f = jdk_new[v["version"]]
        v["url"] = f"https://mirrors.tuna.tsinghua.edu.cn/Adoptium/{v['version']}/jdk/x64/windows/{f}"
        v["file_name"] = f

    # --- PHP: 当前可用版本（7.4 为 vc15）---
    php = find(data, "PHP", "PHP")
    php_new = {
        "8.3.33": "php-8.3.33-Win32-vs16-x64.zip",
        "8.1.34": "php-8.1.34-Win32-vs16-x64.zip",
        "7.4.33": "php-7.4.33-Win32-vc15-x64.zip",
    }
    php["versions"] = [
        {"version": k, "url": f"https://windows.php.net/downloads/releases/{v}", "file_name": v}
        for k, v in php_new.items()
    ]

    # --- Tomcat: 当前可用版本 ---
    tomcat = find(data, "WebServer", "Tomcat")
    tomcat["versions"] = [
        {"version": "9.0.121", "url": "https://dlcdn.apache.org/tomcat/tomcat-9/v9.0.121/bin/apache-tomcat-9.0.121-windows-x64.zip", "file_name": "apache-tomcat-9.0.121-windows-x64.zip"},
        {"version": "10.1.59", "url": "https://dlcdn.apache.org/tomcat/tomcat-10/v10.1.59/bin/apache-tomcat-10.1.59-windows-x64.zip", "file_name": "apache-tomcat-10.1.59-windows-x64.zip"},
    ]

    # --- MySQL: dev.mysql.com 403 -> 官方归档 CDN ---
    mysql = find(data, "Database", "MySQL")
    for v in mysql["versions"]:
        major_minor = ".".join(v["version"].split(".")[:2])
        v["url"] = f"https://cdn.mysql.com/archives/mysql-{major_minor}/{v['file_name']}"

    # --- DBeaver: latest 动态链接（永不过期）---
    dbeaver = find(data, "DBTools", "DBeaver")
    dbeaver["versions"] = [
        {"version": "最新版", "url": "https://dbeaver.io/files/dbeaver-ce-latest-windows-x86_64.zip", "file_name": "dbeaver-ce-latest-windows-x86_64.zip"}
    ]

    # --- HeidiSQL: GitHub releases v12.21 ---
    heidi = find(data, "DBTools", "HeidiSQL")
    heidi["default_source"] = "GitHub"
    heidi["versions"] = [
        {"version": "12.21.0.7344", "url": "https://github.com/HeidiSQL/HeidiSQL/releases/download/v12.21/HeidiSQL_12.21.0.7344_Setup.exe", "file_name": "HeidiSQL_12.21.0.7344_Setup.exe"}
    ]

    # --- Redis 管理器: v1.0.0 资产 404 -> v1.7.4 ---
    rdm = find(data, "Cache", "Redis Another Manager")
    rdm["versions"] = [
        {"version": "1.7.4", "url": "https://github.com/qishibo/AnotherRedisDesktopManager/releases/download/v1.7.4/Another-Redis-Desktop-Manager-win-1.7.4-x64.exe", "file_name": "Another-Redis-Desktop-Manager-win-1.7.4-x64.exe"}
    ]

    # --- Navicat: 官方 17 + 反爬 -> 官网下载页 ---
    navicat = find(data, "DBTools", "Navicat Premium")
    navicat["versions"] = [
        {"version": "17", "url": "https://www.navicat.com.cn/download/navicat-premium", "file_name": "navicat17_premium_cs_x64.exe", "remark": "官网下载页（需在页面选择并下载）"}
    ]

    # newline="\n"：避免 Windows 默认写出 CRLF，导致整个数据文件出现无意义 diff。
    with open(DATA, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=2) + "\n")
    print(f"[OK] 已更新 {DATA}")


if __name__ == "__main__":
    main()
