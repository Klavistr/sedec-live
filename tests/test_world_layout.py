import json
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
        self.assertEqual(dimensions["main"]["maxX"], dimensions["sub"]["minX"])
        self.assertEqual(dimensions["main"]["maxX"], dimensions["corridor"]["minX"])
        self.assertEqual(dimensions["corridor"]["maxY"], dimensions["sub"]["minY"])

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
        self.assertEqual(
            program["runtimeControls"]["volumePresetsDb"],
            [-12.0, -6.0, 0.0, 6.0, 12.0],
        )
        self.assertEqual(program["runtimeControls"]["volumeScope"], "local-user")

    def test_portal_is_scaffolded_without_committing_to_a_transport(self):
        portal = self.layout["mediaTopology"]["portalBridge"]
        self.assertEqual(portal["playerCount"], 0)
        self.assertEqual(portal["outputs"], ["portal"])
        self.assertEqual(portal["status"], "scaffold-only")
        self.assertEqual(portal["transport"], "to-be-validated")

    def test_portal_is_on_the_wall_opposite_the_salon_screen(self):
        dimensions = self.layout["dimensions"]["sub"]
        screen = self.layout["screens"]["sub"]
        portal = self.layout["screens"]["portal"]
        self.assertEqual(screen["faces"], "west")
        self.assertEqual(portal["faces"], "east")
        self.assertGreater(screen["center"][0], portal["center"][0])
        self.assertAlmostEqual(portal["center"][0], dimensions["minX"] + 0.24)

    def test_program_url_is_injected_only_into_the_temporary_build(self):
        config_source = STREAMING_CONFIG_PATH.read_text(encoding="utf-8")
        self.assertIn('ConfigFileName = "streaming.local.json"', config_source)
        self.assertIn("uri.Scheme != Uri.UriSchemeHttps", config_source)
        self.assertIn('EndsWith(".m3u8"', config_source)
        self.assertIn("player.playOnAwakeObject = playOnAwake", config_source)
        self.assertIn("player.autoplay = true", config_source)
        self.assertIn("AddStringPersistentListener", config_source)
        self.assertIn("(redacted, {receiverUrl.Length} characters)", config_source)

        scene = WORLD_SCENE_PATH.read_text(encoding="utf-8")
        self.assertNotIn("https://example.invalid/live/viewer-key/index.m3u8", scene)

    def test_windows_preview_includes_external_diagnostics_launcher(self):
        builder = PREVIEW_BUILDER_PATH.read_text(encoding="utf-8")
        self.assertIn("launch-castle-of-ideas-debug.cmd", builder)
        self.assertIn("watch-castle-of-ideas-log.ps1", builder)
        self.assertIn("Player.log", builder)
        self.assertIn('$_ -notmatch ""\\[Cohtml\\]""', builder)

    def test_native_program_control_actions_are_assigned_back_to_cck(self):
        setup_path = (
            WORLD_PATH
            / "unity"
            / "Assets"
            / "CastleOfIdeas"
            / "Editor"
            / "CastleOfIdeasWorldSetup.cs"
        )
        setup = setup_path.read_text(encoding="utf-8")
        method = setup.split("private static void ConfigureNativeButton", 1)[1]
        method = method.split("private static Text CreateUiText", 1)[0]
        self.assertIn('TrySetField(action, "operations", operations);', method)
        self.assertIn('TrySetField(interactable, "actions", actions);', method)
        self.assertIn('TrySetEnumField(operation, "type", "MethodCall");', method)

        scene = WORLD_SCENE_PATH.read_text(encoding="utf-8")
        for object_name in (
            "ReloadStreamButton",
            "VolumeMinus12DbButton",
            "VolumeMinus6DbButton",
            "Volume0DbButton",
            "VolumePlus6DbButton",
            "VolumePlus12DbButton",
        ):
            self.assertIn(f"m_Name: {object_name}", scene)
        self.assertNotIn("PROGRAM_MEDIA_CONTROLLER", scene)
        self.assertNotIn("VolumeSlider", scene)

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
            + 4
        )
        self.assertEqual(main_seats, 36)
        self.assertEqual(sub_seats, 20)

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

            self.assertNotIn("sedec", str(relative).lower())
            if path.is_file():
                position = path.read_bytes().lower().find(b"sedec")
                self.assertEqual(position, -1, str(relative))


if __name__ == "__main__":
    unittest.main()
