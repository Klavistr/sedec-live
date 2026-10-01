import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WORLD_PATH = ROOT / "cvxr" / "castle-of-ideas"
LAYOUT_PATH = WORLD_PATH / "design" / "layout.json"
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
        self.assertEqual(program["runtimeControls"]["receiverUrl"], "instance-owner")
        self.assertEqual(program["runtimeControls"]["volumeDb"], [-60.0, 12.0])
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

        for path in WORLD_PATH.rglob("*"):
            relative = path.relative_to(WORLD_PATH)
            if any(part in GENERATED_OR_EXTERNAL_PARTS for part in relative.parts):
                continue
            if relative in intentional_integration_notes:
                continue

            self.assertNotIn("sedec", str(relative).lower())
            if path.is_file():
                position = path.read_bytes().lower().find(b"sedec")
                self.assertEqual(position, -1, str(relative))


if __name__ == "__main__":
    unittest.main()
