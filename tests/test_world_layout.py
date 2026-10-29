import json
import hashlib
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORLD_PATH = ROOT / "cvxr" / "castle-of-ideas"
LAYOUT_PATH = WORLD_PATH / "design" / "layout.json"
STREAMING_CONFIG_PATH = (
    WORLD_PATH
    / "unity"
    / "Assets"
    / "CastleOfIdeas"
    / "Editor"
    / "CastleOfIdeasStreamingConfig.cs"
)
PREVIEW_BUILDER_PATH = (
    WORLD_PATH
    / "unity"
    / "Assets"
    / "CastleOfIdeas"
    / "Editor"
    / "CastleOfIdeasPreviewBuilder.cs"
)
WORLD_SCENE_PATH = (
    WORLD_PATH
    / "unity"
    / "Assets"
    / "CastleOfIdeas"
    / "Scenes"
    / "Castle-of-Ideas.unity"
)
GENERATED_OR_EXTERNAL_PARTS = {
    "CVR.CCK",
    "Library",
    "Logs",
    "Temp",
    "UserSettings",
    "__pycache__",
    "build",
}


class WorldLayoutTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.layout = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))

    def test_layout_uses_meter_scale(self):
        self.assertEqual(self.layout["units"], "meters")
        self.assertGreater(self.layout["dimensions"]["wallHeight"], 2.5)

    def test_rooms_and_corridor_connect_at_declared_boundaries(self):
        dimensions = self.layout["dimensions"]
        self.assertEqual(dimensions["main"]["minX"], dimensions["sub"]["maxX"])
        self.assertEqual(dimensions["main"]["minX"], dimensions["corridor"]["maxX"])
        self.assertEqual(dimensions["corridor"]["maxY"], dimensions["sub"]["minY"])

    def test_floor_plan_dimensions_are_derived_at_known_scale(self):
        source = self.layout["sourcePlan"]
        self.assertTrue(source["scaleKnown"])
        self.assertEqual(source["derivedDimensionsMm"]["mainWidth"], 8200)
        self.assertEqual(source["derivedDimensionsMm"]["mainDepth"], 12100)
        self.assertEqual(source["derivedDimensionsMm"]["subWidth"], 6400)
        self.assertEqual(source["derivedDimensionsMm"]["subDepth"], 13800)
        self.assertEqual(source["derivedDimensionsMm"]["corridorWidth"], 1600)
        self.assertEqual(source["derivedDimensionsMm"]["corridorLength"], 13800)
        self.assertIn("not redistributed", source["usage"])

    def test_screens_are_sixteen_by_nine(self):
        for screen in self.layout["screens"].values():
            self.assertAlmostEqual(screen["width"] / screen["height"], 16 / 9, places=3)

    def test_program_feed_is_shared_by_both_lecture_screens(self):
        program = self.layout["mediaTopology"]["programFeed"]
        self.assertEqual(program["playerCount"], 1)
        self.assertEqual(program["outputs"], ["main", "sub"])
        self.assertEqual(program["audioMode"], "mixer-2d")
        self.assertEqual(program["transport"], "https-hls")
        self.assertEqual(
            program["runtimeControls"]["receiverUrl"],
            "build-time-local-config",
        )
        self.assertEqual(program["runtimeControls"]["panel"], "removed-for-early-access")
        self.assertTrue(program["runtimeControls"]["autoplay"])
        self.assertEqual(program["runtimeControls"]["defaultVolumeDb"], 0.0)

    def test_portal_is_scaffolded_without_committing_to_a_transport(self):
        portal = self.layout["mediaTopology"]["portalBridge"]
        self.assertEqual(portal["playerCount"], 0)
        self.assertEqual(portal["outputs"], ["portal"])
        self.assertEqual(portal["status"], "scaffold-only")
        self.assertEqual(portal["transport"], "to-be-validated")

    def test_portal_is_on_main_far_wall_at_big_pad_scale(self):
        dimensions = self.layout["dimensions"]["main"]
        opening = self.layout["openings"]["mainToCorridor"]
        portal = self.layout["screens"]["portal"]
        reference = portal["physicalReference"]
        self.assertEqual(portal["faces"], "west")
        self.assertEqual(opening["wall"], dimensions["minX"])
        self.assertAlmostEqual(portal["center"][0], dimensions["maxX"] - 0.24)
        self.assertAlmostEqual(portal["center"][1], 3.325)
        self.assertEqual(portal["virtualScale"], 2.0)
        self.assertEqual(reference["mount"], "wall")
        self.assertEqual(reference["category"], "Sharp 70-inch BIG PAD")
        self.assertEqual(reference["exactModelStatus"], "to-be-confirmed")
        self.assertAlmostEqual(reference["enclosureWidth"], 1.647)
        self.assertAlmostEqual(reference["enclosureHeight"], 1.01)

    def test_program_url_is_injected_only_into_the_temporary_build(self):
        config_source = STREAMING_CONFIG_PATH.read_text(encoding="utf-8")
        self.assertIn('ConfigFileName = "streaming.local.json"', config_source)
        self.assertIn("uri.Scheme != Uri.UriSchemeHttps", config_source)
        self.assertIn('EndsWith(".m3u8"', config_source)
        self.assertIn("player.playOnAwakeObject = playOnAwake", config_source)
        self.assertIn("player.autoplay = true", config_source)
        self.assertIn("(redacted, {receiverUrl.Length} characters)", config_source)

        scene = WORLD_SCENE_PATH.read_text(encoding="utf-8")
        self.assertNotIn("https://example.invalid/live/viewer-key/index.m3u8", scene)

    def test_windows_preview_includes_external_diagnostics_launcher(self):
        builder = PREVIEW_BUILDER_PATH.read_text(encoding="utf-8")
        self.assertIn("launch-castle-of-ideas-debug.cmd", builder)
        self.assertIn("watch-castle-of-ideas-log.ps1", builder)
        self.assertIn("Player.log", builder)
        self.assertIn('$_ -notmatch ""\\[Cohtml\\]""', builder)

    def test_windows_launcher_dispatch_is_outside_guard_blocks(self):
        source = PREVIEW_BUILDER_PATH.read_text(encoding="utf-8")
        method = source.split("private static string CreateWindowsLauncher", 1)[1]
        launcher = method.split('return $@"',1)[1].split('";',1)[0].replace('""','"')
        depth, dispatches = 0, 0
        for line in launcher.splitlines():
            if "chilloutvr://" in line:
                self.assertEqual(depth,0,"Protocol dispatch was trapped inside a guard block")
                dispatches += 1
            syntax = re.sub(r'"[^"]*"', '', line)
            for char in syntax:
                if char == "(": depth += 1
                elif char == ")": depth -= 1
                self.assertGreaterEqual(depth,0,"Unmatched closing command block")
        self.assertEqual(depth,0,"Unclosed Windows command block")
        self.assertEqual(dispatches,1)

    def test_rc_world_has_no_media_panel(self):
        scene = WORLD_SCENE_PATH.read_text(encoding="utf-8")
        for name in ("PROGRAM_CONTROL_PANEL", "PROGRAM_MEDIA_CONTROL_UI",
                     "ReloadStreamButton", "ReceiverStatus", "VolumeSlider"):
            self.assertFalse(f"m_Name: {name}" in scene, f"Removed media control remains: {name}")

    def test_initial_seating_capacity_is_nontrivial(self):
        furniture = self.layout["furniture"]
        main_seats = (
            len(furniture["main"]["tableColumns"])
            * len(furniture["main"]["tableRows"])
            * furniture["main"]["chairsPerTable"]
        )
        sub_seats = (
            len(furniture["sub"]["tableCentersX"])
            * furniture["sub"]["chairsPerLongSide"]
            * 2
        )
        self.assertEqual(main_seats, 12)
        self.assertEqual(sub_seats, 12)
        self.assertEqual(furniture["main"]["tableSize"][0], 1.8)
        self.assertEqual(furniture["main"]["chairEdgeInset"], 0.15)

    def test_primary_spawn_is_inside_elevator_lobby(self):
        lobby = self.layout["dimensions"]["elevatorLobby"]
        spawn = self.layout["spawns"]["primary"]
        x, y, _ = spawn["position"]
        self.assertEqual(spawn["name"], "SPAWN_PRIMARY_EV")
        self.assertLess(lobby["minX"], x)
        self.assertLess(x, lobby["maxX"])
        self.assertLess(lobby["minY"], y)
        self.assertLess(y, lobby["maxY"])

    def test_elevator_is_a_walkable_cabin(self):
        lobby = self.layout["dimensions"]["elevatorLobby"]
        self.assertGreaterEqual(lobby["maxX"] - lobby["minX"], 2.4)
        self.assertGreaterEqual(lobby["maxY"] - lobby["minY"], 2.5)
        self.assertGreaterEqual(lobby["height"], 3.0)

    def test_displays_fit_below_the_three_meter_ceiling(self):
        height = self.layout["dimensions"]["wallHeight"]
        self.assertEqual(height, 3.0)
        self.assertEqual(self.layout["dimensions"]["elevatorLobby"]["height"], height)
        for screen in self.layout["screens"].values():
            self.assertLess(screen["center"][2] + screen["height"] / 2, height)
            self.assertGreater(screen["center"][2] - screen["height"] / 2, 0)
        main = self.layout["screens"]["main"]
        self.assertAlmostEqual((main["width"] ** 2 + main["height"] ** 2) ** 0.5 / 0.0254, 165, places=2)
        self.assertEqual(main["width"], self.layout["screens"]["sub"]["width"])
        self.assertEqual(main["height"], self.layout["screens"]["sub"]["height"])

    def test_measured_plan_and_open_rear_lecture_space(self):
        dims = self.layout["dimensions"]
        self.assertAlmostEqual(dims["main"]["maxX"] - dims["main"]["minX"], 8.2)
        self.assertAlmostEqual(dims["main"]["maxY"] - dims["main"]["minY"], 12.1)
        self.assertAlmostEqual(dims["sub"]["maxX"] - dims["sub"]["minX"], 13.8)
        self.assertAlmostEqual(dims["sub"]["maxY"] - dims["sub"]["minY"], 6.4)
        self.assertAlmostEqual(dims["corridor"]["maxY"] - dims["corridor"]["minY"], 1.6)
        self.assertAlmostEqual(self.layout["spawns"]["primary"]["position"][0], (dims["sub"]["minX"] + dims["sub"]["maxX"]) / 2)
        fireplace = self.layout["fireplace"]
        self.assertAlmostEqual(fireplace["center"][1] - fireplace["width"] / 2 - dims["corridor"]["maxY"], 1.4)
        for row in self.layout["furniture"]["main"]["tableRows"]:
            self.assertGreater(row - 0.72 - 0.3, dims["corridor"]["maxY"])

    def test_photo_textures_match_their_provenance(self):
        directory = WORLD_PATH / "unity/Assets/CastleOfIdeas/Textures/PhotoDerived"
        provenance = json.loads((directory / "texture-provenance.json").read_text())
        recipes_path = WORLD_PATH / "design/photo-textures.json"
        self.assertEqual(provenance["recipeSha256"], hashlib.sha256(recipes_path.read_bytes()).hexdigest())
        self.assertFalse(provenance["generatedImagery"])
        for record in provenance["sources"]:
            name = record["recipe"]["name"]
            for suffix, hash_key in ((".png", "textureSha256"), ("-normal.png", "normalSha256")):
                self.assertEqual(hashlib.sha256((directory / (name + suffix)).read_bytes()).hexdigest(), record[hash_key])

    def test_alice_textures_match_their_historical_sources(self):
        directory = WORLD_PATH / "unity/Assets/CastleOfIdeas/Textures/AliceLiterature"
        provenance = json.loads((directory / "literature-provenance.json").read_text())
        self.assertEqual(provenance["selectedFont"], "Hoefler Text")
        self.assertFalse(provenance["fontRedistributed"])
        self.assertEqual(len(provenance["records"]), 10)
        source_dir = WORLD_PATH / "references/alice"
        for record in provenance["records"]:
            source = source_dir / record.get("source", "pg11-source.txt")
            output = directory / (record["name"] + ".png")
            self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), record["sourceSha256"])
            self.assertEqual(hashlib.sha256(output.read_bytes()).hexdigest(), record["outputSha256"])
            if record["kind"] == "text":
                self.assertTrue((WORLD_PATH / record["excerpt"]).is_file())

    def test_measured_alice_planks_and_traceable_panorama(self):
        floor = self.layout["aliceFloor"]
        self.assertEqual(floor["plankWidth"], 0.1515)
        self.assertEqual(floor["plankLength"], 0.909)
        self.assertEqual(floor["stagger"], 0.5)
        panorama = json.loads((WORLD_PATH / "design/alice-wallpaper.json").read_text())
        self.assertEqual(len(panorama["placements"]), 42)
        self.assertEqual(panorama["pageCount"], 19)
        self.assertAlmostEqual(panorama["pageSizePixels"][0] / panorama["pageSizePixels"][1], 2**-.5, places=3)
        self.assertFalse(panorama["repeatedText"])
        self.assertFalse(panorama["repeatedIllustrations"])
        self.assertEqual(len({p["number"] for p in panorama["placements"]}), 42)
        pages = {p["number"]:p for p in panorama["pages"]}
        for illustration in panorama["placements"]:
            self.assertEqual(illustration["chapter"], pages[illustration["page"]]["chapter"])
        ends = {}
        for page in panorama["pages"]:
            self.assertEqual(page["lens"]["type"],"smooth-wave-refraction")
            self.assertGreater(page["lens"]["jacobianMinimum"],.28)
            self.assertEqual(page["lens"]["edgeMapping"],"identity")
            self.assertEqual(page["wordStart"],ends.get(page["chapter"],0))
            self.assertGreater(page["wordEnd"],page["wordStart"])
            ends[page["chapter"]] = page["wordEnd"]
            excerpt = (WORLD_PATH/page["excerpt"]).read_text()
            self.assertNotIn("*",excerpt)
            self.assertNotIn("[Illustration]",excerpt)
        self.assertEqual(panorama["contourClearanceMeters"], .025)
        self.assertEqual(panorama["wallOrder"], ["north", "east", "south", "west"])
        for name, digest in panorama["sourceHashes"].items():
            source = WORLD_PATH / "references/alice" / name
            self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), digest)

    def test_generic_world_assets_do_not_contain_sedec_branding(self):
        intentional_integration_notes = {Path("README.md")}
        local_only_settings = {
            Path("streaming.local.json"),
            Path("streaming.local.json.meta"),
        }

        for path in WORLD_PATH.rglob("*"):
            relative = path.relative_to(WORLD_PATH)
            if any(part in GENERATED_OR_EXTERNAL_PARTS for part in relative.parts):
                continue
            if relative in intentional_integration_notes:
                continue
            if relative in local_only_settings:
                continue
            if relative.parts[0:2] == ("unity", "Assets") and path.name.startswith("cvrworld_"):
                continue
            if relative.parts[0:2] == ("preview", "photo-review"):
                continue

            self.assertNotIn("sedec", str(relative).lower())
            if path.is_file():
                position = path.read_bytes().lower().find(b"sedec")
                self.assertEqual(position, -1, str(relative))


if __name__ == "__main__":
    unittest.main()
