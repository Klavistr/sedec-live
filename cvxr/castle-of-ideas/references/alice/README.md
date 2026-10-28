# Alice reference sources

Retrieved 2026-10-28 from Project Gutenberg. Keep the original inputs here so
each derived texture can be traced and rebuilt without an image generator.

| File | Work / source |
| --- | --- |
| `pg11-source.txt` | Lewis Carroll, *Alice's Adventures in Wonderland* (1865), [eBook 11](https://www.gutenberg.org/ebooks/11), text prepared by Arthur DiBianca and David Widger |
| `white-rabbit.gif` | John Tenniel, *White Rabbit checking watch*, [illustration 2, eBook 114](https://www.gutenberg.org/files/114/114-h/114-h.htm) |
| `cheshire-cat.gif` | John Tenniel, *Alice speaks to Cheshire Cat*, illustration 23, same collection |
| `tea-party.gif` | John Tenniel, *Mad Tea Party*, illustration 25, same collection |
| `illustrations-license.txt` | [Source rights statement](https://www.gutenberg.org/files/114/LICENSE.txt) |

Project Gutenberg identifies the text and this illustration collection as public
domain in the USA. The original authors are Lewis Carroll (1832–1898) and John
Tenniel (1820–1914). Use only these historical text/illustration sources, not modern
adaptations, translations, or the venue's modern illustrated wallpaper.

Modifications: the illustrations are converted to a dark-ink/cream-paper palette
and proportionally resized without inventing content. Text excerpts are reflowed
and typeset separately in the operator-selected **Hoefler Text** installed on the
Mac. Exact excerpt text is retained in `excerpts/`; font file and source/output
hashes are recorded in the generated `literature-provenance.json`. No font files
are redistributed and no additional font license is asserted.

Run from the repository root with a Python interpreter that has Pillow:

```bash
"$PHOTO_PYTHON" cvxr/castle-of-ideas/blender/create_alice_assets.py \
  --build --font 'Hoefler Text' \
  --font-proof cvxr/castle-of-ideas/preview/alice-font-candidates.png
```

The download URLs are recorded in the source script and generated provenance.
The full source text retains its original Project Gutenberg notice; runtime wall
textures use only the selected novel passages and the historical illustrations.

The runtime wallpaper uses 19 two-column page masters in ISO B proportions
(width/height = 1/sqrt(2)), fitted horizontally by less than 0.3% to the 40.4 m
perimeter at 3 m height. Each page has its own smooth wave-refraction field,
with local magnification and shear but no radial focus or vanishing point;
the rectangular page edges remain straight. The field retains a positive
Jacobian to prevent folds and radial streaks. All 12 chapters supply distinct,
non-repeated passages. All 42 original Tenniel illustrations are used once,
only on a page belonging to the same chapter. Chapter assignments, original URLs
and hashes are in `illustrations.json`; `blender/fetch_alice_illustrations.py`
fetches these source files using curl and the operating system certificate store.

Text follows outer ink contours with 2.5 cm clearance before distortion.
Asterisk/rule-only separator rows and illustration placeholders are removed from
the derived text; the unmodified source retains them. No modern venue artwork
is copied. Four distinct wall cuts cover all sides, with the south door cut out
in geometry. Page numbers, chapter numbers, non-overlapping source word ranges,
exact excerpt files, illustration assignments, lens parameters, source/font hashes
and wall crop offsets are in `design/alice-wallpaper.json` and runtime provenance.
Separate illustration/text layers, individual pages and the full canvas are saved
locally in ignored `preview/alice-wallpaper/` for review and editing.
