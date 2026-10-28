# Photograph-derived venue textures

Source: venue reference photographs supplied by the project operator for this
world, 2026-10-28. Original names and SHA-256 hashes are recorded in
`texture-provenance.json`; crop recipes are in `design/photo-textures.json`
relative to the world project. No external stock library or image-generation
engine was used. This task authorizes use of the supplied photographs for the
world; no general-purpose third-party asset license is asserted.

The original HEIC files remain with the operator. EXIF is normalized before
cropping; exported PNGs contain no camera/location metadata. Faces, presentation
slides, seasonal decorations and desk letter medallions are outside the selected
regions. The complete reference photographs and annotated review copies are
local-only and are not included in the Unity project or public repository.

Processing is fully scripted: crop or quadrilateral rectification, resize,
reflection, repeated masonry layout, exposure adjustment, and approximate normal
maps derived from luminance gradients. Photo lighting remains visible and these
are not calibrated PBR scans. Normal maps are approximations, not measured depth.

Run `blender/extract_photo_textures.py --help` from the world project to regenerate.
The source HEIC hashes distinguish the exact inputs; the texture and normal hashes
allow outputs to be checked without redistributing full photographs. Keep the
original HEICs backed up outside this repository.
