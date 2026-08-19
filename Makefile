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
UV ?= uv
UV_RUN := $(UV) run

CHANGELOG ?=
UPLOAD_FLAGS ?=

GUTENBERG_CACHE := cache/gutenberg
DOWNLOAD_STAMP := $(GUTENBERG_CACHE)/.stamp
CATALOG := mod/assets/liberterra/config/liberterra-catalog.json
# Hand-written UI strings merged into the generated en.json; edits here must rebuild it.
LANG_OVERLAYS := $(wildcard mod/lang/en-*.json)

.PHONY: help download assets build package deploy install run deploy-run upload-moddb test test-tools test-game refresh bump-version bump-version-files bump-minor-version bump-patch-version docs-build docs-serve docs-figures

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
	@printf "  make deploy        Bump the patch version, then install into Vintage Story Mods\n"
	@printf "  make install       Install without touching the version\n"
	@printf "  make run           Launch Vintage Story\n"
	@printf "  make deploy-run    Deploy then launch\n"
	@printf "  make upload-moddb  Upload dist zip to mods.vintagestory.at\n"
	@printf "                     (needs VSMODDB_SESSION + CHANGELOG='...' )\n"
	@printf "  make docs-figures  Regenerate the docs layout figures from the mod source\n"
	@printf "  make docs-build    Build the ProperDocs site into site/ (uv)\n"
	@printf "  make docs-serve    Serve the docs locally (uv)\n"
	@printf "  make bump-patch-version  Increment patch version, build, and install\n"
	@printf "  make bump-minor-version  Increment minor version, reset patch to 0, build, and install\n"
	@printf "  make bump-version VERSION=0.3.0  Set an exact version, build, and install\n"

# Two suites, split by what they cover: the Python pipeline that writes the assets, and the C# that
# the game runs. Both are unit tests — no network, no world, no launching Vintage Story.
test: test-tools test-game

test-tools:
	@$(UV_RUN) python -m unittest discover -s tests -t tests

# Compiles the mod and asserts against the committed assets, so this stays a unit test: no Gutenberg
# pull and no asset regeneration. Use `make build` when the generated assets need to be current.
test-game:
	@$(DOTNET) build mod/LiberTerra.csproj -c $(CONFIGURATION) -v minimal --nologo
	@$(DOTNET) run --project tests/loottables/LootTablesCheck.csproj -c $(CONFIGURATION) -v minimal --nologo

# Real file targets, so an unchanged tree skips the Python pipeline entirely
# instead of walking all 75 works and rewriting 512 assets on every compile.
# Inputs are the scripts and the work list; use `make refresh` to force a pull.
$(DOWNLOAD_STAMP): tools/download_texts.py tools/mvp_works.py
	@$(UV_RUN) python tools/download_texts.py
	@mkdir -p "$(GUTENBERG_CACHE)"
	@touch "$@"

$(CATALOG): $(DOWNLOAD_STAMP) tools/build_lore_assets.py tools/mvp_works.py $(LANG_OVERLAYS)
	@$(UV_RUN) python tools/build_lore_assets.py

download: $(DOWNLOAD_STAMP)

assets: $(CATALOG)

refresh:
	@$(UV_RUN) python tools/download_texts.py --force
	@mkdir -p "$(GUTENBERG_CACHE)"
	@touch "$(DOWNLOAD_STAMP)"
	@$(UV_RUN) python tools/build_lore_assets.py

build: assets
	@$(DOTNET) build mod/LiberTerra.csproj -c $(CONFIGURATION) -v minimal

package: build
	@mkdir -p "$(DIST_DIR)"
	@rm -f "$(PACKAGE_FILE)"
	@cd "$(BUILD_OUTPUT_DIR)" && zip -qr "$(CURDIR)/$(PACKAGE_FILE)" .
	@printf "Packaged $(PACKAGE_FILE)\n"

# Every deploy ships a version nobody has seen before, so a build sitting in the Mods folder
# can never claim a number that is already tagged or published. Reach for install when you
# want the same version reinstalled — deploy always moves the patch number.
deploy: bump-patch-version

# The raw install, with the version left exactly as it is. bump-version already wrote the
# number it wants before it gets here, so it installs through this and not through deploy,
# which would bump a second time on top of it.
install: build
	@rm -rf "$(DEPLOY_DIR)"
	@mkdir -p "$(MODS_DIR)"
	@cp -R "$(BUILD_OUTPUT_DIR)" "$(DEPLOY_DIR)"
	@printf "Deployed to $(DEPLOY_DIR)\n"

run:
	@open -a "$(GAME_APP)"

deploy-run: deploy run

docs-build docs-serve docs-figures: SHELL := /bin/sh

# Both figures are read out of the mod source — the icons from the Cairo bar tables the game
# draws with, the shapes from the shipped layout config — so the docs cannot show a pile the
# block does not build.
docs-figures:
	@$(UV_RUN) python tools/render_layout_icons.py
	@$(UV_RUN) python tools/render_layout_shapes.py
	@$(UV_RUN) python tools/render_transform_diagram.py

docs-build: docs-figures
	@$(UV_RUN) properdocs build -f properdocs.yml --strict

docs-serve: docs-figures
	@$(UV_RUN) properdocs serve -f properdocs.yml

# Requires VSMODDB_SESSION (vs_websessionkey cookie) and CHANGELOG='...' or
# CHANGELOG pointing at nothing while using UPLOAD_FLAGS='--changelog-file notes.md'.
upload-moddb: package
	@if [ -z "$$VSMODDB_SESSION" ] && [ ! -f .env ]; then \
		printf "Set VSMODDB_SESSION to your mods.vintagestory.at vs_websessionkey cookie.\n" >&2; \
		exit 1; \
	fi
	@if [ -n "$(CHANGELOG)" ]; then \
		$(UV_RUN) python tools/upload_moddb.py --changelog "$(CHANGELOG)" $(UPLOAD_FLAGS); \
	else \
		$(UV_RUN) python tools/upload_moddb.py $(UPLOAD_FLAGS); \
	fi

# The version lives in two source files that must never drift: modinfo.json is what the
# game and ModDB read, LiberTerraModMetadata.Version is what the mod logs about itself.
# GitHub issue templates also show the current number as a placeholder. Bump all of
# those together, then install so the running game reports the new number. Installing
# rather than deploying is what stops deploy's own patch bump from landing on top of this one.
bump-version: bump-version-files install

bump-version-files:
	@if [[ -z "$(VERSION)" ]]; then printf "Usage: make bump-version VERSION=0.2.1\n"; exit 2; fi
	@if ! [[ "$(VERSION)" =~ ^[0-9]+\.[0-9]+\.[0-9]+$$ ]]; then printf "VERSION must look like 0.2.1\n"; exit 2; fi
	@perl -0pi -e 's/"version":\s*"[^"]+"/"version": "$(VERSION)"/' mod/modinfo.json
	@perl -0pi -e 's/public const string Version = "[^"]+";/public const string Version = "$(VERSION)";/' mod/src/LiberTerraModMetadata.cs
	@for f in .github/ISSUE_TEMPLATE/*.yml; do \
		perl -0pi -e 's/(id: mod-version.*?placeholder:\s*)v?[0-9]+\.[0-9]+\.[0-9]+/$${1}v$(VERSION)/s' "$$f"; \
	done
	@printf "Bumped Liber Terra source version to $(VERSION)\n"

bump-minor-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' mod/modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from mod/modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$(( $$parts[2] + 1 )).0"; \
	$(MAKE) bump-version VERSION=$$new_version

bump-patch-version:
	@current=$$(perl -0ne 'print $$1 if /"version":\s*"([0-9]+\.[0-9]+\.[0-9]+)"/' mod/modinfo.json); \
	if [[ -z "$$current" ]]; then printf "Could not read version from mod/modinfo.json\n"; exit 2; fi; \
	parts=("$${(@s:.:)current}"); \
	new_version="$$parts[1].$$parts[2].$$(( $$parts[3] + 1 ))"; \
	$(MAKE) bump-version VERSION=$$new_version
