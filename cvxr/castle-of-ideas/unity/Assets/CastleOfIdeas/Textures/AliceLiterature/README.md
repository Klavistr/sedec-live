# Separate Alice illustration and text textures

The three Tenniel illustration plates and three Hoefler Text body-text plates are
independent textures. Source files, credits, rights statement and exact excerpts
are in `references/alice/` relative to the world project. Rebuild with
`blender/create_alice_assets.py --build --font 'Hoefler Text'`.

`literature-provenance.json` records original URLs, source/output SHA-256 hashes,
excerpt offsets, and the selected local font hash. The font itself is not copied.
Processing consists of palette mapping, resampling, and typesetting; no AI image
generation or content-aware reconstruction is used.

The world uses the four `wallpaper-*.png` strips of one full-room panorama.
Text follows the outer ink contour with a 2.5 cm pre-warp clearance. The composed
layers undergo a shared displacement field with varying local magnification,
producing lens-like expansion, compression and strong undulation after typesetting.
The six original plates remain editable source derivatives. The layout seed,
placements and lens parameters are recorded in `design/alice-wallpaper.json`.
