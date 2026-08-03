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

.PHONY: help download assets build package deploy run deploy-run

help:
	@printf "Targets:\n"
	@printf "  make download    Fetch MVP Gutenberg texts into cache/\n"
	@printf "  make assets      Build lore JSON + lang from cache\n"
	@printf "  make build       Build the Liber Terra mod\n"
	@printf "  make package     Zip the mod into dist/\n"
	@printf "  make deploy      Install into Vintage Story Mods\n"
	@printf "  make run         Launch Vintage Story\n"
	@printf "  make deploy-run  Deploy then launch\n"

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
