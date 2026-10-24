using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ReengageSpeedLinesPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReusesOneMeshBehindActorsWithoutChangingTheCameraOrMaterial()
        {
            var shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader == null || !shader.isSupported) Assert.Ignore("The project URP silhouette shader is required.");
            var parent = new GameObject("Speed Line Test");
            var cameraObject = new GameObject("Speed Line Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(0f, 1f, -10f);
            var material = new Material(shader);
            material.SetColor("_Tint", Color.magenta);
            var lines = new DuelReengageSpeedLines(parent.transform, material, 30);
            try
            {
                var renderer = parent.GetComponentInChildren<MeshRenderer>();
                var filter = parent.GetComponentInChildren<MeshFilter>();
                Mesh mesh = filter.sharedMesh;
                Vector3 cameraPosition = camera.transform.position;
                lines.Tick(camera, Vector3.zero, 1, .65f, .02f, 0);
                Assert.That(lines.Visible, Is.True);
                Assert.That(renderer.sortingOrder, Is.EqualTo(-4));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(renderer.gameObject.layer, Is.EqualTo(30));
                Assert.That(mesh.vertexCount, Is.EqualTo(DuelReengageSpeedLines.StreakCount * 4));
                Assert.That(material.GetColor("_Tint"), Is.EqualTo(Color.magenta));
                Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
                for (int i = 0; i < 12; i++) lines.Tick(camera, Vector3.zero, -1, .65f, .02f, 0);
                Assert.That(filter.sharedMesh, Is.SameAs(mesh));
                Assert.That(parent.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
                Assert.That(mesh.bounds.size.x, Is.LessThan(camera.orthographicSize * camera.aspect * 2.5f));
                lines.Tick(camera, Vector3.zero, 0, 0f, .02f, 0);
                Assert.That(lines.Visible, Is.False);
                lines.Tick(camera, Vector3.zero, 1, .65f, .02f, 0);
                Assert.That(lines.Visible, Is.True);
                lines.Tick(camera, Vector3.zero, 1, .65f, float.NaN, 0);
                Assert.That(lines.Visible, Is.False);
                lines.Tick(camera, Vector3.zero, 1, .65f, .02f, 0);
                lines.Clear();
                Assert.That(lines.Visible, Is.False);
                lines.Dispose();
                Assert.That(lines.Visible, Is.False);
                yield return null;
                Assert.That(filter == null, Is.True);
                Assert.That(mesh == null, Is.True);
                Assert.That(material != null, Is.True, "The arena, rather than the effect, owns its shared material.");
            }
            finally
            {
                lines.Dispose();
                Object.Destroy(parent);
                Object.Destroy(cameraObject);
                Object.Destroy(material);
            }
        }

        [UnityTest]
        public IEnumerator ArenaTickShowsOnlyReengagementAfterContactAndClearsAtTurnAndBattleBoundaries()
        {
            var shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader == null || !shader.isSupported) Assert.Ignore("The project URP silhouette shader is required.");
            var parent = new GameObject("Reengage Arena Test");
            var art = new LegacyDuelArt();
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            JsonUtility.FromJsonOverwrite("{\"movementSpeedMultiplier\":1,\"reengageSpeedLinesEnabled\":true,\"reengageSpeedLinesStrength\":0.65}", settings);
            LegacyArenaView arena = null;
            try
            {
                arena = LegacyArenaView.Create(parent.transform, art, settings);
                MeshRenderer lines = FindLines(parent);
                Assert.That(lines, Is.Not.Null);
                arena.Reset();
                BeginLongApproach(arena);
                arena.Tick(.02f, .02f);
                arena.Tick(.02f, .02f);
                Assert.That(lines.enabled, Is.False, "The first approach has no preceding clash.");
                arena.ObserveCombatContact();
                BeginLongApproach(arena);
                arena.Tick(.02f, .02f);
                arena.Tick(.02f, .02f);
                Assert.That(lines.enabled, Is.True, "The same movement now closes a gap after combat contact.");
                arena.Tick(0f, .02f);
                Assert.That(lines.enabled, Is.False, "A held simulation clock cannot produce movement streaks.");
                arena.Tick(.02f, .02f);
                Assert.That(lines.enabled, Is.True);
                arena.EndTurn();
                Assert.That(lines.enabled, Is.False);
                arena.Reset();
                BeginLongApproach(arena);
                arena.Tick(.02f, .02f);
                arena.Tick(.02f, .02f);
                Assert.That(lines.enabled, Is.False, "A new battle forgets the previous battle's contact.");
                yield return null;
            }
            finally
            {
                arena?.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(parent);
            }
        }

        private static void BeginLongApproach(LegacyArenaView arena)
        {
            Vector3 player = arena.PlayerRenderer.transform.localPosition;
            Vector3 enemy = arena.EnemyRenderer.transform.localPosition;
            player.x = -8f; enemy.x = 8f;
            arena.PlayerRenderer.transform.localPosition = player;
            arena.EnemyRenderer.transform.localPosition = enemy;
            arena.BeginApproach();
        }

        private static MeshRenderer FindLines(GameObject parent)
        {
            foreach (var renderer in parent.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == "Duel Reengage Speed Lines") return renderer;
            return null;
        }

        [UnityTest]
        public IEnumerator MissingMaterialLeavesNoVisibleStreaks()
        {
            var parent = new GameObject("Unavailable Speed Line Test");
            var cameraObject = new GameObject("Unavailable Speed Line Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var lines = new DuelReengageSpeedLines(parent.transform, null, 30);
            try
            {
                foreach (float delta in new[] { .02f, 0f, float.NaN, float.PositiveInfinity })
                {
                    lines.Tick(camera, Vector3.zero, 1, 1f, delta, 0);
                    Assert.That(lines.Visible, Is.False);
                }
                lines.Clear();
                yield return null;
            }
            finally
            {
                lines.Dispose();
                Object.Destroy(parent);
                Object.Destroy(cameraObject);
            }
        }
    }
}
