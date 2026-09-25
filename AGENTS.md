# AGENTS.md

## Project context

This repository contains production assets and helper tools used on the streaming operator's Mac for SEDEC livestreaming and recording. Keep event-wide operations and tone-of-voice documentation in `Klavistr/sedec-d11n`; keep OBS configuration, media, local fallback screens, CVXR, and 3D production assets here. Services, streaming infrastructure, and browser-source pages served from the VPS belong in `Klavistr/sedec-server`.

## Before changing files

1. Read `README.md` and the README in the directory being changed.
2. Run `git status --short` and preserve unrelated user changes.
3. Prefer a small, inspectable source file over committing generated output.

## Commands

- Bootstrap: `make setup`
- Repository checks: `make check`
- Unit tests: `make test`
- Local fallback preview: `make serve`

The helper tooling must continue to work with Python 3.11+ and the standard library unless a dependency is justified and documented.

## Conventions

- Use kebab-case ASCII filenames unless an upstream application requires another name.
- Keep local fallback screens usable at 1920x1080 and responsive when practical.
- Refer to deployed browser sources through stable `sedec-server` URLs, not GitHub raw URLs or sibling-repository paths.
- Use repository-relative paths for OBS media references whenever OBS permits it.
- Do not commit stream keys, credentials, personal data, caches, or machine-specific absolute paths.
- Do not add large binaries until Git LFS is installed and the tracking policy is documented.
- Keep editable sources; avoid committing reproducible renders, caches, and build directories.
- Record third-party asset origin, author, license, and modifications next to the asset.
- Do not edit sibling repositories as part of a task in this repository unless the user explicitly asks.

## Verification

Run `make check test` after changing repository structure or Python tooling. For a local fallback change, also run `make serve` and inspect the affected page at its intended OBS viewport.
