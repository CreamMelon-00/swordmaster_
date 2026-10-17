using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class EnemyRosterHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThreeIndividuals_ShowTheirOwnGaugesAndOwnerNumbers_AndTabHighlightsTheMatchingOpponent()
        {
            yield return null;
            using (var scope = new Scope())
            {
                scope.Refresh();
                Assert.That(scope.Hud.Root.transform.Find("EnemyStatus").gameObject.activeSelf, Is.False);
                Assert.That(scope.Hud.HighlightedEnemyIndex, Is.EqualTo(scope.Duel.EnemyOwnerAtSlot(0)));
                for (int index = 0; index < 3; index++)
                {
                    RectTransform panel = scope.Hud.GetEnemyStatusRoot(index);
                    Assert.That(panel, Is.Not.Null);
                    Assert.That(panel.Find("Enemy Number Plate/Enemy Number").GetComponent<Text>().text, Is.EqualTo((index + 1).ToString()));
                    Assert.That(panel.Find("HP").GetComponent<Image>().fillAmount, Is.EqualTo(1f));
                    Assert.That(panel.Find("Resistance").GetComponent<Image>().fillAmount, Is.EqualTo(1f));
                    for (int previous = 0; previous < index; previous++)
                        Assert.That(LocalRect(panel).Overlaps(LocalRect(scope.Hud.GetEnemyStatusRoot(previous))), Is.False,
                            "All three individual gauges remain readable when their actors stand close together. " + scope.LayoutDetails());
                    AssertNearOwnHead(scope, panel, scope.Arena.EnemyActorTransforms[index], new Vector3(-.4f, 1f, 0f));
                }
                for (int slot = 0; slot < scope.Duel.EnemyQueue.Count; slot++)
                {
                    RectTransform mark = scope.Hud.GetEnemyQueueOwnerMark(slot);
                    Assert.That(mark, Is.Not.Null);
                    Assert.That(mark.Find("Enemy Number").GetComponent<Text>().text,
                        Is.EqualTo((scope.Duel.EnemyOwnerAtSlot(slot) + 1).ToString()));
                    Assert.That(mark.anchoredPosition.y, Is.LessThan(0f), "Owner identity leaves the upper keyword tabs free.");
                }
                scope.Hud.SetInspectedSlot(1);
                scope.Refresh();
                Assert.That(scope.Hud.HighlightedEnemyIndex, Is.EqualTo(scope.Duel.EnemyIndexForSlot(1)));
                Assert.That(scope.Hud.GetEnemyStatusRoot(scope.Hud.HighlightedEnemyIndex).Find("Active Enemy").gameObject.activeSelf, Is.True);
                scope.Hud.SetInspectedSlot(-1);
                scope.Duel.Commit();
                scope.Duel.BeginNextSlot();
                scope.Duel.ResolveNextHit();
                scope.Hud.PlayEnemyQueueCancellation();
                scope.Refresh();
                for (int index = 0; index < 3; index++)
                    Assert.That(scope.Hud.GetEnemyStatusRoot(index).Find("Resistance").GetComponent<Image>().fillAmount,
                        Is.EqualTo(scope.Duel.Enemies[index].Resistance / (float)scope.Duel.Enemies[index].MaxResistance));
            }
        }

        [UnityTest]
        public IEnumerator OneBrokenOwner_ShatterIsIdempotent_AndHolesKeepTheirOriginalOpposingSlotPositions()
        {
            yield return null;
            using (var scope = new Scope())
            {
                scope.Duel.Commit();
                scope.Duel.BeginNextSlot();
                scope.Refresh();
                int owner = scope.Duel.ActiveEnemyIndex;
                scope.Duel.ResolveNextHit();
                scope.Hud.PlayEnemyQueueCancellation();
                int canceled = 0;
                for (int slot = 1; slot < scope.Duel.EnemyQueue.Count; slot++)
                    if (scope.Duel.IsEnemySlotCanceled(slot)) canceled++;
                Assert.That(canceled, Is.EqualTo(2));
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(canceled));
                scope.Hud.PlayEnemyQueueCancellation();
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(canceled));
                scope.Refresh();
                Assert.That(scope.Hud.GetQueuedSkillAnchor(false, 0), Is.Not.Null, "The current skill still completes.");
                for (int slot = 1; slot < scope.Duel.EnemyQueue.Count; slot++)
                {
                    RectTransform mine = scope.Hud.GetQueuedSkillAnchor(true, slot);
                    RectTransform theirs = scope.Hud.GetQueuedSkillAnchor(false, slot);
                    Assert.That(mine, Is.Not.Null);
                    if (scope.Duel.EnemyOwnerAtSlot(slot) == owner)
                        Assert.That(theirs, Is.Null, "Only that individual's future skills leave the shared queue.");
                    else
                    {
                        Assert.That(theirs, Is.Not.Null);
                        Assert.That(theirs.anchoredPosition.x, Is.EqualTo(-mine.anchoredPosition.x).Within(.01f),
                            "A cancellation leaves an empty logical slot; another enemy never slides into a different pairing.");
                    }
                }
                scope.Hud.BeginTurn();
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.Zero);
                Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ReturningToOneOpponent_RestoresTheExistingSingleStatusAndRemovesOwnerDecorations()
        {
            yield return null;
            using (var scope = new Scope())
            {
                scope.Refresh();
                var single = new LegacyQueuedDuel();
                scope.Refresh(single);
                Assert.That(scope.Hud.Root.transform.Find("EnemyStatus").gameObject.activeSelf, Is.True);
                for (int index = 0; index < 3; index++) Assert.That(scope.Hud.GetEnemyStatusRoot(index), Is.Null);
                for (int slot = 0; slot < single.EnemyQueue.Count; slot++) Assert.That(scope.Hud.GetEnemyQueueOwnerMark(slot), Is.Null);
                Assert.That(scope.Hud.HighlightedEnemyIndex, Is.EqualTo(-1));
            }
        }

        [UnityTest]
        public IEnumerator IdleTrio_IndividualPanelsClearTheOtherFightersFacesAsWellAsEachOther()
        {
            yield return null;
            using (var scope = new Scope())
            {
                scope.Refresh();
                for (int index = 0; index < 3; index++)
                    AssertClearOfFaces(scope, scope.Hud.GetEnemyStatusRoot(index));
            }
        }

        [UnityTest]
        public IEnumerator ActualTrioHud_RendersAtThreeAspectRatios_WithVisibleNonOverlappingIndividualPanels()
        {
            yield return null;
            string output = Environment.GetEnvironmentVariable("SWORDMASTER_TRIO_HUD_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
            using (var scope = new Scope())
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(1920, 810) })
                {
                    scope.Resize(size);
                    yield return null;
                    scope.Refresh();
                    for (int index = 0; index < 3; index++)
                    {
                        RectTransform panel = scope.Hud.GetEnemyStatusRoot(index);
                        Rect rect = LocalRect(panel);
                        RectTransform canvas = (RectTransform)scope.Hud.Root.transform;
                        Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-canvas.rect.width * .5f));
                        Assert.That(rect.xMax, Is.LessThanOrEqualTo(canvas.rect.width * .5f));
                        Assert.That(rect.yMax, Is.LessThanOrEqualTo(canvas.rect.height * .5f));
                        for (int previous = 0; previous < index; previous++)
                            Assert.That(rect.Overlaps(LocalRect(scope.Hud.GetEnemyStatusRoot(previous))), Is.False,
                                size + " " + scope.LayoutDetails());
                        AssertNearOwnHead(scope, panel, scope.Arena.EnemyActorTransforms[index], new Vector3(-.4f, 1f, 0f));
                        AssertClearOfFaces(scope, panel);
                    }
                    RectTransform playerStatus = (RectTransform)scope.Hud.Root.transform.Find("PlayerStatus");
                    AssertNearOwnHead(scope, playerStatus, scope.Arena.PlayerRenderer.transform, new Vector3(-.6f, 1.1f, 0f));
                    Color32[] shown = scope.Capture(string.IsNullOrEmpty(output) ? null
                        : Path.Combine(output, size.x + "x" + size.y + "-trio-planning.png"));
                    var panels = new RectTransform[3];
                    for (int index = 0; index < 3; index++)
                    {
                        panels[index] = scope.Hud.GetEnemyStatusRoot(index);
                        panels[index].gameObject.SetActive(false);
                    }
                    Color32[] hidden = scope.Capture(null);
                    for (int index = 0; index < 3; index++) panels[index].gameObject.SetActive(true);
                    int changed = 0;
                    for (int pixel = 0; pixel < shown.Length; pixel++)
                        if (Math.Abs(shown[pixel].r - hidden[pixel].r) + Math.Abs(shown[pixel].g - hidden[pixel].g) +
                            Math.Abs(shown[pixel].b - hidden[pixel].b) > 12) changed++;
                    Assert.That(changed, Is.GreaterThan(300), "The three native status panels contribute visible pixels at this viewport.");
                }
            }
        }

        private static Rect LocalRect(RectTransform panel)
            => new Rect(panel.anchoredPosition - Vector2.Scale(panel.sizeDelta, panel.pivot), panel.sizeDelta);

        private static void AssertNearOwnHead(Scope scope, RectTransform panel, Transform actor, Vector3 localHead)
        {
            Camera camera = scope.Arena.ArenaCamera;
            Vector3 head = camera.WorldToScreenPoint(actor.TransformPoint(localHead));
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            float scale = scope.Hud.Root.GetComponent<Canvas>().scaleFactor;
            float horizontalGap = Mathf.Abs((bottomLeft.x + topRight.x) * .5f - head.x);
            float verticalGap = bottomLeft.y - head.y;
            Assert.That(horizontalGap, Is.LessThan(264f * scale),
                "A head panel stays beside its own fighter, rather than clamping to a remote screen edge. " + scope.LayoutDetails());
            Assert.That(verticalGap, Is.InRange(8f * scale, 210f * scale),
                "The status remains just above its actor, with room for overlapping neighbours to stack upward. " + scope.LayoutDetails());
        }

        private static void AssertClearOfFaces(Scope scope, RectTransform panel)
        {
            Camera camera = scope.Arena.ArenaCamera;
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            var panelScreen = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
            float scale = scope.Hud.Root.GetComponent<Canvas>().scaleFactor;
            for (int index = -1; index < 3; index++)
            {
                Transform actor = index < 0 ? scope.Arena.PlayerRenderer.transform : scope.Arena.EnemyActorTransforms[index];
                // The idle sprites' hair/face region, measured in the rendered view, is smaller than the HUD's padded
                // avoidance region. This checks the picture the player sees, independent of panel-to-panel spacing.
                Vector3 hair = camera.WorldToScreenPoint(actor.TransformPoint(new Vector3(index < 0 ? -.6f : -.4f, 1.4f, 0f)));
                var face = new Rect(hair.x - 48f * scale, hair.y - 90f * scale, 96f * scale, 90f * scale);
                Assert.That(panelScreen.Overlaps(face), Is.False,
                    "An individual status panel leaves fighter " + index + "'s hair and face visible. " + scope.LayoutDetails());
            }
        }

        [Serializable]
        private sealed class PanelEvidence
        {
            public int index;
            public Vector2 anchoredPosition, size, pivot;
            public Vector3 localScale;
            public Rect rect;
        }

        [Serializable]
        private sealed class LayoutEvidence
        {
            public Rect canvasRect;
            public float canvasScaleFactor;
            public Vector3 canvasScale;
            public PanelEvidence[] panels;
            public Vector3[] enemyActorPositions;
        }

        private sealed class Scope : IDisposable
        {
            private readonly GameObject host = new GameObject("Enemy Roster HUD Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public LegacyQueuedDuel Duel { get; }
            public LegacyArenaView Arena { get; }
            public LegacyCombatHud Hud { get; }
            public Scope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                Arena = LegacyArenaView.Create(host.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.Reset();
                Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                Hud = new LegacyCombatHud(host.transform, art, _ => { }, () => { }, () => { }, settings);
                Hud.SetEnemyActors(Arena.EnemyActorTransforms);
                LegacySkill player = Attack(88901, "검증 베기", 12);
                LegacySkill enemy = Attack(88902, "검증 공격", 1);
                var script = new EnemyScript(new[] { new[] { enemy, enemy, enemy } });
                Duel = new LegacyQueuedDuel(1000, 1000, new[] { player }, new[]
                {
                    new LegacyEnemyDefinition("견습생", 1000, 5, script),
                    new LegacyEnemyDefinition("검술생", 1000, 5, script),
                    new LegacyEnemyDefinition("수련생", 1000, 5, script)
                }, randomSeed: 7, meshPercent: 0);
                for (int slot = 0; slot < 9; slot++) Assert.That(Duel.TryQueueLane(0), Is.True);
                Resize(new Vector2Int(1280, 720));
            }
            private static LegacySkill Attack(int id, string name, int power)
                => new LegacySkill(id, name, 0, power, power, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
            public void Resize(Vector2Int size)
            {
                Camera camera = Arena.ArenaCamera;
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.aspect = size.x / (float)size.y;
                Canvas canvas = Hud.Root.GetComponent<Canvas>();
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(1f, camera.nearClipPlane + .1f);
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f);
                float logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }
            public void Refresh(ILegacyDuelView view = null)
            {
                view = view ?? Duel;
                bool resolving = view.Phase == LegacyDuelPhase.Resolving;
                if (view is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1) Arena.SelectActiveEnemy(roster.ActiveEnemyIndex);
                for (int frame = 0; frame < 12; frame++)
                {
                    if (!resolving) Arena.SetPlanningState(10f, false, true);
                    Arena.SetHudFloor(Hud.QueuePanelFloor);
                    Arena.Tick(0f, .02f);
                    Hud.Refresh(view, 10f, resolving, view.CurrentSlot?.SlotIndex ?? -1, Arena.ArenaCamera,
                        Arena.PlayerRenderer.transform, Arena.EnemyRenderer.transform, 0f, 0f);
                    Canvas.ForceUpdateCanvases();
                }
            }
            public Color32[] Capture(string path)
            {
                foreach (Transform node in host.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Arena.ArenaCamera.cullingMask = 1 << 31;
                Canvas.ForceUpdateCanvases();
                Arena.ArenaCamera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture old = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    texture.Apply();
                    if (!string.IsNullOrEmpty(path))
                    {
                        File.WriteAllBytes(path, texture.EncodeToPNG());
                        File.WriteAllText(Path.ChangeExtension(path, ".json"), LayoutDetails());
                    }
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public string LayoutDetails()
            {
                var panels = new PanelEvidence[3];
                var actors = new Vector3[3];
                for (int index = 0; index < 3; index++)
                {
                    RectTransform panel = Hud.GetEnemyStatusRoot(index);
                    panels[index] = panel == null ? null : new PanelEvidence
                    {
                        index = index, anchoredPosition = panel.anchoredPosition, size = panel.sizeDelta,
                        pivot = panel.pivot, localScale = panel.localScale, rect = LocalRect(panel)
                    };
                    actors[index] = Arena.EnemyActorTransforms[index].position;
                }
                Canvas canvas = Hud.Root.GetComponent<Canvas>();
                return JsonUtility.ToJson(new LayoutEvidence
                {
                    canvasRect = ((RectTransform)Hud.Root.transform).rect, canvasScaleFactor = canvas.scaleFactor,
                    canvasScale = canvas.transform.lossyScale, panels = panels, enemyActorPositions = actors
                }, true);
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Hud.Dispose(); Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(host);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
