# ============================================================
#  DevBox - build / release entry point
#  Run `make` (or `make help`) for the target list.
#
#  KEEP THIS FILE PURE ASCII. GNU Make on Windows transcodes
#  non-ASCII text read from the makefile, so UTF-8 comments and
#  strings come out as mojibake in `make help`.
#  Check: LC_ALL=C grep -c '[^ -~	]' Makefile    # must be 0
# ============================================================

# --- Windows shell bootstrap (do not remove) -----------------------------
# Git for Windows ships sh.exe / rm.exe / mkdir.exe in <Git>\usr\bin, but only
# <Git>\cmd (git.exe only) is on PATH by default. With no sh.exe on PATH, GNU
# Make silently falls back to cmd.exe: recipes needing a POSIX shell break and
# `echo "x"` prints the quotes literally.
# Locate <Git>\usr\bin with make builtins only - a $(shell ...) probe would
# emit errors on the shells where it does not apply, and `2>NUL` under sh
# leaves a stray file named NUL. No-op on Linux/macOS and where sh is on PATH.
#
# Load-bearing here: clean/installer/docs use rm, `[ -d ]`, command -v, `$$()`.
ifeq ($(OS),Windows_NT)
  sp       := $(subst ,, )
  GIT_ROOT := $(patsubst %/cmd,%,$(firstword $(filter %/cmd,$(subst ;, ,$(subst \,/,$(PATH))))))
  ifeq ($(wildcard $(GIT_ROOT)/usr/bin/sh.exe),)
    GIT_ROOT := $(firstword $(foreach d,C:/Program$(sp)Files/Git C:/Program$(sp)Files$(sp)(x86)/Git,\
                  $(if $(wildcard $(d)/usr/bin/sh.exe),$(d))))
  endif
  ifneq ($(GIT_ROOT),)
    PATH_SEP := $(if $(findstring ;,$(PATH)),;,:)
    SHELL    := $(GIT_ROOT)/usr/bin/sh.exe
    export PATH := $(GIT_ROOT)/usr/bin$(PATH_SEP)$(PATH)
  endif
endif

# ============================================================
#  CONFIG - keep in sync with .github/workflows/release.yml
# ============================================================
APP_ID      := DevBox
COMPANY     := FNSoftware
CSPROJ      := DevBox/DevBox.csproj
ICON        := DevBox/Resources/icon.ico
RUNTIME     := win-x64
FRAMEWORK   := net10-x64-desktop
VPK_VER     := 1.2.0

PUBLISH_DIR := publish/$(RUNTIME)
VPACK_DIR   := publish/velopack

# Version of record: the VERSION file at the repo root (csproj/CHANGELOG must
# match it - see `make verify`). $(file ...) reads it without spawning a shell.
VER := $(strip $(file <VERSION))

# PowerShell host for the .ps1 helpers: PS7 when installed, else Windows
# PowerShell 5.1. Override with `make <target> PS=pwsh`.
PS ?= $(if $(wildcard C:/Program$(sp)Files/PowerShell/7/pwsh.exe),pwsh,powershell)

# Overridable knobs (never set as environment variables by these recipes).
CONFIG ?= Release
ARGS   ?=

# ============================================================
.DEFAULT_GOAL := help

.PHONY: help run run-host ui publish installer verify release docs clean

HOST_CSPROJ := DevBox.Host/DevBox.Host.csproj

help:
	@echo "DevBox - available commands"
	@echo "$(if $(VER),  current version: $(VER),[warn] VERSION file missing)"
	@echo ""
	@echo "  make run          Run Avalonia app from source ($(CONFIG)) [legacy]"
	@echo "  make run-host     Run WinForms+WebView2 host (API :35174)"
	@echo "  make ui           Run React Vite HMR UI (:35173)"
	@echo "  make publish      Single-file framework-dependent publish -> $(PUBLISH_DIR)/"
	@echo "  make installer    publish + vpk pack -> Velopack Setup.exe in $(VPACK_DIR)/"
	@echo "  make verify       Check VERSION / DevBox.csproj / CHANGELOG agree"
	@echo "  make release      verify, then tag v<VER> and push (starts the CI release)"
	@echo "  make docs         VitePress dev server for docs/ (npm install on first run)"
	@echo "  make clean        Remove root bin/ obj/ publish output/ RELEASE_NOTES.md DevBox-*.zip"
	@echo ""
	@echo "  make publish ARGS=-Zip    also emit DevBox-<ver>-$(RUNTIME).zip"
	@echo "  make run CONFIG=Debug     run a Debug build"

# ------------------------------------------------------------
#  Targets that earn their keep: multi-step pipelines, long flag sets, fixed
#  artifact paths, a fixed project path. Deliberately absent are the
#  single-command wrappers - `build: dotnet build -c Release`,
#  `restore: dotnet restore`, `tidy: dotnet format` - they save zero
#  keystrokes while adding a layer that goes stale. Type those directly.
# ------------------------------------------------------------

# Fixed project path + configuration: nobody should have to remember
# `--project DevBox/DevBox.csproj` or which config the README uses.
run:
	@dotnet run --project $(CSPROJ) -c $(CONFIG)

# New shell: WinForms + WebView2 + local API on 127.0.0.1:35174
run-host:
	@dotnet run --project $(HOST_CSPROJ) -c $(CONFIG)

# React UI with Vite HMR on 127.0.0.1:35173 (proxy /api -> 35174)
ui:
	@[ -d ui/node_modules ] || { echo "ui deps missing - running npm install (first run only)"; cd ui && npm install; }
	@cd ui && npm run dev

# scripts/publish.ps1 already does clean + publish + strip pdb + size report.
# This target exists to make it callable from bash/cmd as well as PowerShell,
# and to give the long flag set (`ARGS=-Zip`) one documented entry point.
publish:
	@$(PS) -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1 $(ARGS)
	@echo "[ok] published -> $(PUBLISH_DIR)"

# publish -> vpk pack: the local Velopack round trip (CI does the real one).
# The vpk flag set mirrors release.yml; keep the two in sync.
installer: publish
	@vpk=$$(command -v vpk 2>/dev/null); \
	if [ -z "$$vpk" ]; then \
		echo "[skip] vpk CLI not found - install it with:"; \
		echo "         dotnet tool install -g vpk --version $(VPK_VER)"; \
		echo "       then put %USERPROFILE%\.dotnet\tools on PATH"; \
		exit 0; \
	fi; \
	[ -n "$(VER)" ] || { echo "[error] VERSION file missing or empty"; exit 1; }; \
	[ -f RELEASE_NOTES.md ] || printf '## %s %s\n' "$(APP_ID)" "$(VER)" > RELEASE_NOTES.md; \
	echo "vpk pack -u $(APP_ID) -v $(VER) -r $(RUNTIME) -p $(PUBLISH_DIR) -o $(VPACK_DIR)"; \
	"$$vpk" pack \
		-u "$(APP_ID)" \
		-v "$(VER)" \
		-r "$(RUNTIME)" \
		-p "$(PUBLISH_DIR)" \
		-o "$(VPACK_DIR)" \
		-e "$(APP_ID).exe" \
		--packTitle "$(APP_ID)" \
		--packAuthors "$(COMPANY)" \
		-i "$(ICON)" \
		--framework "$(FRAMEWORK)" \
		--releaseNotes RELEASE_NOTES.md || exit 1; \
	echo "[ok] installer -> $(VPACK_DIR)/$(APP_ID)-win-Setup.exe"

# Three files must agree before a tag is cut; the CI workflow re-checks the
# same thing and fails the release if they drift.
verify:
	@$(PS) -NoProfile -ExecutionPolicy Bypass -File scripts/verify-version.ps1 $(ARGS)

# Outward-facing and hard to undo: pushes the tag that triggers release.yml.
# Refuses a dirty tree (an uncommitted version bump would tag the wrong
# commit) and refuses to reuse an existing tag.
release: verify
	@git diff --quiet && git diff --cached --quiet || { echo "[error] working tree is dirty - commit the version bump first"; exit 1; }
	@git rev-parse -q --verify "refs/tags/v$(VER)" >/dev/null && { echo "[error] tag v$(VER) already exists"; exit 1; } || true
	@git tag -a "v$(VER)" -m "$(APP_ID) v$(VER)"
	@echo "[ok] tagged v$(VER)"
	@git push origin "v$(VER)"
	@echo "[ok] pushed v$(VER) - GitHub Actions is now building the release"

# Two steps: first-run dependency install, then the dev server.
# No URL is echoed on purpose - the dev port shifts when 5173 is taken and the
# site base is /DevBox.Net/ (docs/.vitepress/config), so vitepress's own banner
# is the only reliable place to read it from.
docs:
	@[ -d docs/node_modules ] || { echo "docs deps missing - running npm install (first run only)"; cd docs && npm install; }
	@echo "[ok] starting vitepress dev"
	@cd docs && npm run dev

# Only removes what this makefile produces, and only gitignored paths.
# publish/ is left alone apart from the two generated subdirs: the icon
# scripts write publish/_icon_check, which is not ours to delete.
clean:
	@rm -rf bin obj "$(PUBLISH_DIR)" "$(VPACK_DIR)" artifacts RELEASE_NOTES.md DevBox-*.zip
	@echo "[ok] cleaned build output"
