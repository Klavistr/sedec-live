PYTHON ?= python3
VENV := .venv
VENV_PYTHON := $(VENV)/bin/python
BLENDER ?= /Applications/Blender.app/Contents/MacOS/Blender
CVXR_WORLD := cvxr/castle-of-ideas

.PHONY: setup check test serve cvxr-world clean

setup: $(VENV_PYTHON)

$(VENV_PYTHON):
	$(PYTHON) -m venv $(VENV)
	$(VENV_PYTHON) -m pip --version

check: setup
	$(VENV_PYTHON) scripts/check_repo.py

test: setup
	$(VENV_PYTHON) -m unittest discover -s tests -v

serve: setup
	$(VENV_PYTHON) -m http.server 8080 --directory assets/overlays

cvxr-world:
	"$(BLENDER)" --background --factory-startup \
		--python $(CVXR_WORLD)/blender/generate_world.py -- \
		--output $(CVXR_WORLD)/blender/castle-of-ideas.blend \
		--export-fbx $(CVXR_WORLD)/unity/Assets/CastleOfIdeas/Models/castle-of-ideas.fbx \
		--render $(CVXR_WORLD)/preview/blockout.png \
		--render-elevator $(CVXR_WORLD)/preview/elevator-interior.png

clean:
	@echo "Remove generated files manually after reviewing them; .venv is intentionally preserved."
