SHELL := /bin/zsh

# Local convenience only: borrow the astra_terra SDK when it happens to be
# installed. CI provisions its own via actions/setup-dotnet and the runner is
# this same Mac, so never let the probe shadow it there.
ASTRA_DOTNET := /Users/lalmei/projects/astra_terra/.dotnet
ifeq ($(CI),)
  ASTRA_DOTNET_BIN := $(wildcard $(ASTRA_DOTNET)/dotnet)
endif

ifneq ($(ASTRA_DOTNET_BIN),)
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

GUTENBERG_CACHE := cache/gutenberg
DOWNLOAD_STAMP := $(GUTENBERG_CACHE)/.stamp
CATALOG := mod/assets/liberterra/config/liberterra-catalog.json
# Hand-written UI strings merged into the generated en.json; edits here must rebuild it.
LANG_OVERLAYS := $(wildcard mod/lang/en-*.json)

.PHONY: help download assets build package deploy run deploy-run upload-moddb test test-tools test-game refresh

help:
	@printf "Targets:\n"
	@printf "  make test          Run every unit test (tools + game)\n"
	@printf "  make test-tools    Unit-test the asset pipeline (no network, no game)\n"
	@printf "  make test-game     Unit-test the mod code (needs the game DLLs)\n"
	@printf "  make download      Fetch MVP Gutenberg texts into cache/\n"
	@printf "  make assets        Build lore JSON + lang from cache\n"
	@printf "  make refresh       Force re-download and regenerate assets\n"
	@printf "  make build         Build the Liber Terra mod\n"
	@printf "  make package       Zip the mod into dist/\n"
	@printf "  make deploy        Install into Vintage Story Mods\n"
	@printf "  make run           Launch Vintage Story\n"
	@printf "  make deploy-run    Deploy then launch\n"
	@printf "  make upload-moddb  Upload dist zip to mods.vintagestory.at\n"
	@printf "                     (needs VSMODDB_SESSION + CHANGELOG='...' )\n"

# Two suites, split by what they cover: the Python pipeline that writes the assets, and the C# that
# the game runs. Both are unit tests — no network, no world, no launching Vintage Story.
test: test-tools test-game

test-tools:
	@$(PYTHON) -m unittest discover -s tests -t tests

# Compiles the mod and asserts against the committed assets, so this stays a unit test: no Gutenberg
# pull and no asset regeneration. Use `make build` when the generated assets need to be current.
test-game:
	@$(DOTNET) build mod/LiberTerra.csproj -c $(CONFIGURATION) -v minimal --nologo
	@$(DOTNET) run --project tests/loottables/LootTablesCheck.csproj -c $(CONFIGURATION) -v minimal --nologo

# Real file targets, so an unchanged tree skips the Python pipeline entirely
# instead of walking all 75 works and rewriting 512 assets on every compile.
# Inputs are the scripts and the work list; use `make refresh` to force a pull.
$(DOWNLOAD_STAMP): tools/download_texts.py tools/mvp_works.py
	@$(PYTHON) tools/download_texts.py
	@mkdir -p "$(GUTENBERG_CACHE)"
	@touch "$@"

$(CATALOG): $(DOWNLOAD_STAMP) tools/build_lore_assets.py tools/mvp_works.py $(LANG_OVERLAYS)
	@$(PYTHON) tools/build_lore_assets.py

download: $(DOWNLOAD_STAMP)

assets: $(CATALOG)

refresh:
	@$(PYTHON) tools/download_texts.py --force
	@mkdir -p "$(GUTENBERG_CACHE)"
	@touch "$(DOWNLOAD_STAMP)"
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
