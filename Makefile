SHELL := /bin/zsh

ASTRA_DOTNET := /Users/lalmei/projects/astra_terra/.dotnet
ifneq ($(wildcard $(ASTRA_DOTNET)/dotnet),)
  DOTNET_CLI_HOME := $(CURDIR)/.dotnet-home
  DOTNET_ENV := PATH="$(ASTRA_DOTNET):$$PATH" DOTNET_CLI_HOME="$(DOTNET_CLI_HOME)" DOTNET_CLI_TELEMETRY_OPTOUT=1
  DOTNET := $(DOTNET_ENV) $(ASTRA_DOTNET)/dotnet
else
  DOTNET_ENV := DOTNET_CLI_TELEMETRY_OPTOUT=1
  DOTNET := $(DOTNET_ENV) dotnet
endif

CONFIGURATION ?= Release
TARGET_FRAMEWORK := net10.0
GAME_APP ?= /Applications/Vintage Story.app
MODS_DIR ?= $(HOME)/Library/Application Support/VintagestoryData/Mods
DEPLOY_DIR := $(MODS_DIR)/LiberTerra
BUILD_OUTPUT_DIR := mod/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)
DIST_DIR := dist
MOD_VERSION = $(shell perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' mod/modinfo.json)
PACKAGE_FILE = $(DIST_DIR)/LiberTerra-$(MOD_VERSION).zip
PYTHON ?= python3

CHANGELOG ?=
UPLOAD_FLAGS ?=

.PHONY: help download assets build package deploy run deploy-run upload-moddb

help:
	@printf "Targets:\n"
	@printf "  make download      Fetch MVP Gutenberg texts into cache/\n"
	@printf "  make assets        Build lore JSON + lang from cache\n"
	@printf "  make build         Build the Liber Terra mod\n"
	@printf "  make package       Zip the mod into dist/\n"
	@printf "  make deploy        Install into Vintage Story Mods\n"
	@printf "  make run           Launch Vintage Story\n"
	@printf "  make deploy-run    Deploy then launch\n"
	@printf "  make upload-moddb  Upload dist zip to mods.vintagestory.at\n"
	@printf "                     (needs VSMODDB_SESSION + CHANGELOG='...' )\n"

download:
	@$(PYTHON) tools/download_texts.py

assets: download
	@$(PYTHON) tools/build_lore_assets.py

build: assets
	@$(DOTNET) build mod/LiberTerra.csproj -c $(CONFIGURATION) -v minimal

package: build
	@mkdir -p "$(DIST_DIR)"
	@rm -f "$(PACKAGE_FILE)"
	@cd "$(BUILD_OUTPUT_DIR)" && zip -qr "$(CURDIR)/$(PACKAGE_FILE)" .
	@printf "Packaged $(PACKAGE_FILE)\n"

deploy: build
	@rm -rf "$(DEPLOY_DIR)"
	@mkdir -p "$(MODS_DIR)"
	@cp -R "$(BUILD_OUTPUT_DIR)" "$(DEPLOY_DIR)"
	@printf "Deployed to $(DEPLOY_DIR)\n"

run:
	@open -a "$(GAME_APP)"

deploy-run: deploy run

# Requires VSMODDB_SESSION (vs_websessionkey cookie) and CHANGELOG='...' or
# CHANGELOG pointing at nothing while using UPLOAD_FLAGS='--changelog-file notes.md'.
upload-moddb: package
	@if [ -z "$$VSMODDB_SESSION" ] && [ ! -f .env ]; then \
		printf "Set VSMODDB_SESSION to your mods.vintagestory.at vs_websessionkey cookie.\n" >&2; \
		exit 1; \
	fi
	@if [ -n "$(CHANGELOG)" ]; then \
		$(PYTHON) tools/upload_moddb.py --changelog "$(CHANGELOG)" $(UPLOAD_FLAGS); \
	else \
		$(PYTHON) tools/upload_moddb.py $(UPLOAD_FLAGS); \
	fi
