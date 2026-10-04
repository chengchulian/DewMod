using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DewSuperSmart
{
    internal static class Program
    {
        private static readonly ThreatSourceKind Source = ThreatSourceKind.EnemyCast;
        private static readonly ThreatActivity Active = ThreatActivity.Active;

        private static int _passed;

        private static ThreatZone Circle(Vector3 center, float radius)
        {
            return ThreatZone.Circle(null, null, center, radius, Source, Active, 1f, 0f, true);
        }

        private static ThreatZone Line(Vector3 origin, Vector3 direction, float length, float width)
        {
            return ThreatZone.Line(null, null, null, origin, direction, length, width, Source, Active, 1f, 0f, true);
        }

        private static void Main()
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            Run("circle escape includes hero radius and clearance", TestCircleEscape);
            Run("line escape includes hero radius and clearance", TestLineEscape);
            Run("cone signed distance rejects the back side", TestConeGeometry);
            Run("polygon escape clears a convex boundary", TestPolygonEscape);
            Run("outside-circles escape moves into the safe disk", TestOutsideCirclesEscape);
            Run("path safety catches a thin line between coarse samples", TestThinLinePath);
            Run("path safety catches a thin line immediately after the origin", TestThinLineNearOrigin);
            Run("path safety rejects entering a nearby threat", TestOutsidePathCannotCrossThreat);
            Run("path safety allows an overlapping start to leave", TestOverlapCanLeave);
            Run("path safety permits escaping two overlapping circles", TestTwoOverlappingCirclesCanLeave);
            Run("path safety rejects moving deeper from an overlap", TestOverlapCannotMoveDeeper);
            Run("path safety rejects re-entry into a concave polygon", TestConcaveReentry);
            Run("projectile red window slides along the full trajectory", TestProjectileSlidingWindow);
            ControllerTests.Register(Run);
            TreantPowerBombPreviewTests.Register(Run);
            MonsterRushProjectionTests.Register(Run);

            Console.WriteLine("Regression tests passed: " + _passed);
        }

        private static void TestCircleEscape()
        {
            ThreatZone threat = Circle(Vector3.zero, 2f);
            Vector3 point = Vector3.zero;
            Assert(threat.TryGetEscapePoint(point, 0.5f, 0.2f, out Vector3 escape), "circle should produce an escape point");
            Assert(threat.SignedDistance(escape, 0.5f) >= 0.2f, "circle escape must clear the expanded edge");
            Assert(Math.Abs((escape - point).magnitude - 2.7f) < 0.002f, "circle escape must stop at radius 2 + hero 0.5 + clearance 0.2");

            Vector3 safe = new Vector3(4f, 0f, 0f);
            Assert(threat.TryGetEscapePoint(safe, 0.5f, 0.2f, out Vector3 unchanged), "safe circle point should be accepted");
            Assert((unchanged - safe).magnitude < 0.0001f, "safe point should not receive another movement command");
        }

        private static void TestLineEscape()
        {
            ThreatZone threat = Line(new Vector3(0f, 0f, -5f), Vector3.forward, 10f, 2f);
            Vector3 point = new Vector3(0f, 0f, 0f);
            Assert(threat.TryGetEscapePoint(point, 0.4f, 0.15f, out Vector3 escape), "line should produce an escape point");
            Assert(threat.SignedDistance(escape, 0.4f) >= 0.15f, "line escape must clear the expanded edge");
            Assert(Math.Abs(Math.Abs(escape.x) - 1.55f) < 0.002f, "line escape must stop at half-width 1 + hero 0.4 + clearance 0.15");
        }

        private static void TestProjectileSlidingWindow()
        {
            ThreatZone path = ThreatZone.Line(null, null, null, Vector3.zero, new Vector3(1f, 0f, 0f),
                12f, 0.5f, ThreatSourceKind.Projectile, Active, 1.8f, float.PositiveInfinity, true, 10f);
            ThreatZone initialWindow = ThreatZone.ProjectileWindow(path, Vector3.zero, 12f);
            ThreatZone advancedWindow = ThreatZone.ProjectileWindow(path, new Vector3(3f, 0f, 0f), 9f);

            Assert(path.IsMovingProjectile && !initialWindow.IsMovingProjectile && initialWindow.IsProjectileWindow,
                "the full path must remain temporal while the red window is a geometric obstacle");
            Assert(Math.Abs(initialWindow.Length - 4.5f) < 0.001f &&
                   Math.Abs(advancedWindow.Length - 4.5f) < 0.001f,
                "a 10-unit-per-second projectile must have a 4.5-unit red window");
            Assert(advancedWindow.SignedDistance(new Vector3(1f, 0f, 0f), 0f) > 0f &&
                   advancedWindow.SignedDistance(new Vector3(4f, 0f, 0f), 0f) < 0f,
                "the red window must move with the projectile rather than stay at its launch point");
            Assert(!ThreatPathSafety.IsSafe(new[] { path, initialWindow },
                    new Vector3(2f, 0f, 2f), new Vector3(2f, 0f, -2f), 0.4f),
                "movement across the red window must be rejected");
            Assert(ThreatPathSafety.IsSafe(new[] { path, initialWindow },
                    new Vector3(8f, 0f, 2f), new Vector3(8f, 0f, -2f), 0.4f),
                "the remaining trajectory outside the red window must not become a static obstacle");
        }

        private static void TestConeGeometry()
        {
            ThreatZone threat = ThreatZone.Cone(null, null, Vector3.zero, Vector3.forward, 5f, 60f, Active, 1f, 0f, true);
            Assert(threat.SignedDistance(new Vector3(0f, 0f, 2f), 0f) < 0f, "point in cone must be inside");
            Assert(threat.SignedDistance(new Vector3(0f, 0f, -1f), 0f) > 0f, "point behind cone must be outside");
            Assert(threat.SignedDistance(new Vector3(4f, 0f, 2f), 0f) > 0f, "point beyond cone side must be outside");
            Assert(threat.TryGetEscapePoint(new Vector3(0f, 0f, 2f), 0.35f, 0.2f, out Vector3 escape), "cone should produce an escape point");
            Assert(threat.SignedDistance(escape, 0.35f) >= 0.2f, "cone escape must clear the expanded edge");
            float sideClearance = Math.Abs(escape.x) * (float)Math.Cos(Math.PI / 6d) - escape.z * 0.5f;
            Assert(Math.Abs(sideClearance - 0.55f) < 0.002f, "nearest cone escape must stop hero 0.35 + clearance 0.2 outside a side");
        }

        private static void TestPolygonEscape()
        {
            ThreatZone threat = ThreatZone.Polygon(
                null,
                new[]
                {
                    new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f),
                    new Vector3(2f, 0f, 2f), new Vector3(-2f, 0f, 2f)
                },
                Source, Active, 1f, 0f, true);

            Assert(threat.TryGetEscapePoint(Vector3.zero, 0.4f, 0.2f, out Vector3 escape), "polygon should produce an escape point");
            Assert(threat.SignedDistance(escape, 0.4f) >= 0.2f, "polygon escape must clear the expanded edge");
            Assert(Math.Abs(Math.Max(Math.Abs(escape.x), Math.Abs(escape.z)) - 2.6f) < 0.002f, "square escape must stop at edge 2 + hero 0.4 + clearance 0.2");
        }

        private static void TestOutsideCirclesEscape()
        {
            ThreatZone threat = ThreatZone.OutsideCircles(
                null,
                new[] { Vector3.zero, new Vector3(10f, 0f, 0f) },
                2f,
                Source, Active, 1f, 0f, true);
            Vector3 point = new Vector3(3f, 0f, 0f);
            Assert(threat.SignedDistance(point, 0f) < 0f, "outside-circles point should be in the forbidden outside region");
            Assert(threat.TryGetEscapePoint(point, 0.2f, 0.1f, out Vector3 escape), "outside-circles should find a safe disk");
            Assert(threat.SignedDistance(escape, 0.2f) >= 0.1f, "outside-circles escape must retain the requested margin");
            Assert(Math.Abs(escape.x - 1.7f) < 0.002f, "safe-disk escape must stop at radius 2 - hero 0.2 - clearance 0.1");
        }

        private static void TestThinLinePath()
        {
            ThreatZone threat = Line(new Vector3(0.075f, 0f, -1f), Vector3.forward, 2f, 0.02f);
            bool safe = ThreatPathSafety.IsSafe(
                new List<ThreatZone> { threat },
                new Vector3(-2f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                0f);
            Assert(!safe, "a continuous path must catch a thin line between samples");
        }

        private static void TestOutsidePathCannotCrossThreat()
        {
            ThreatZone threat = Circle(Vector3.zero, 1f);
            bool safe = ThreatPathSafety.IsSafe(
                new List<ThreatZone> { threat },
                new Vector3(-2f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                0.2f);
            Assert(!safe, "an origin outside a nearby threat cannot cross it");
        }

        private static void TestThinLineNearOrigin()
        {
            ThreatZone threat = Line(new Vector3(0.01f, 0f, -1f), Vector3.forward, 2f, 0.01f);
            Assert(!ThreatPathSafety.IsSafe(new[] { threat }, Vector3.zero, new Vector3(0.5f, 0f, 0f), 0f),
                "a safe origin only 0.005 from a thin line must not cross it before the first full sample");
        }

        private static void TestOverlapCanLeave()
        {
            ThreatZone threat = Circle(Vector3.zero, 1.5f);
            bool safe = ThreatPathSafety.IsSafe(
                new List<ThreatZone> { threat },
                new Vector3(1f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                0f);
            Assert(safe, "an overlapping origin may move monotonically out of a threat");
        }

        private static void TestOverlapCannotMoveDeeper()
        {
            ThreatZone threat = Circle(Vector3.zero, 1.5f);
            bool safe = ThreatPathSafety.IsSafe(
                new List<ThreatZone> { threat },
                new Vector3(1f, 0f, 0f),
                Vector3.zero,
                0f);
            Assert(!safe, "an overlapping origin cannot move deeper into a threat");
        }

        private static void TestTwoOverlappingCirclesCanLeave()
        {
            ThreatZone left = Circle(new Vector3(-0.5f, 0f, 0f), 2f);
            ThreatZone right = Circle(new Vector3(0.5f, 0f, 0f), 2f);
            Assert(ThreatPathSafety.IsSafe(new[] { left, right }, Vector3.zero, new Vector3(0f, 0f, 4f), 0.8f),
                "moving up between overlapping circle centers continuously leaves both circles");
        }

        private static void TestConcaveReentry()
        {
            ThreatZone threat = ThreatZone.Polygon(
                null,
                new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 1f), new Vector3(1f, 0f, 1f),
                    new Vector3(1f, 0f, 4f), new Vector3(0f, 0f, 4f)
                },
                Source, Active, 1f, 0f, true);
            bool safe = ThreatPathSafety.IsSafe(
                new List<ThreatZone> { threat },
                new Vector3(3.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 3.5f),
                0f);
            Assert(!safe, "a path that exits and re-enters a concave threat must be rejected");
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL " + name + ": " + exception.Message);
                Environment.ExitCode = 1;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
