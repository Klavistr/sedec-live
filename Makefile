PYTHON ?= python3
VENV := .venv
VENV_PYTHON := $(VENV)/bin/python

.PHONY: setup check test serve clean

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

clean:
	@echo "Remove generated files manually after reviewing them; .venv is intentionally preserved."

