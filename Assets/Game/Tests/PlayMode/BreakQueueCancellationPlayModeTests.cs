using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>New resistance breaks discard that side's later reservations, while the current exchange keeps playing.
    /// The real controllers must show the loss at those cards, without repeating it on later hits or moving during pause.</summary>
    public sealed class BreakQueueCancellationPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator ActualCampaignHit_ShatterBelongsOnlyToTheBrokenSide_CurrentMultihitFinishes_AndPauseAndResetAreClean()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                foreach (bool playerBroken in new[] { false, true })
                {
                    LegacyQueuedDuel duel = scope.Prepare(4, playerBroken, cost: 1);
                    for (int i = 0; i < 4; i++) Assert.That(scope.Controller.QueueLane(0), Is.True);
                    int act = duel.Act;
                    scope.Begin();
                    LegacyCurrentSlot current = duel.CurrentSlot;
                    scope.Until(() => current.HitsResolved > 0);
                    Assert.That((playerBroken ? duel.Player : duel.Enemy).IsResistanceBroken, Is.True);
                    Assert.That((playerBroken ? duel.Enemy : duel.Player).IsResistanceBroken, Is.False);
                    Assert.That(playerBroken ? duel.PlayerBreakCanceledFromSlotIndex : duel.EnemyBreakCanceledFromSlotIndex,
                        Is.EqualTo(1));
                    Assert.That(current.HitsResolved, Is.EqualTo(1), "Only the first of the current skill's four hits has landed.");
                    Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(3));
                    Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.EqualTo(3));
                    Assert.That(duel.Act, Is.EqualTo(act), "A break cancels reservations without returning their ACT.");
                    Assert.That(scope.Hud.GetQueuedSkillAnchor(playerBroken, 0), Is.Not.Null, "The current card remains in the clash.");
                    for (int i = 1; i < 4; i++)
                    {
                        Assert.That(scope.Hud.GetQueuedSkillAnchor(playerBroken, i), Is.Null, "The broken side's future card is gone.");
                        Assert.That(scope.Hud.GetQueuedSkillAnchor(!playerBroken, i), Is.Not.Null, "The opponent keeps its reservation.");
                    }
                    AssertFragmentsOnSide(scope.Hud, playerBroken);
                    scope.Hud.PlayBreakQueueCancellation(playerBroken, 1);
                    scope.Hud.PlayBreakQueueCancellation(playerBroken, 2);
                    Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(3), "The same cancellation is never replayed.");

                    DuelQueueShatterGraphic shell = FirstFragment(scope.Hud);
                    float progress = shell.Progress;
                    Assert.That(scope.Controller.PauseBattle(), Is.True);
                    scope.Advance(.2f);
                    Assert.That(shell.Progress, Is.EqualTo(progress), "The pause menu holds the fragment clock too.");
                    Assert.That(scope.Controller.ResumeBattle(), Is.True);
                    scope.Hud.AdvanceBreakQueueFragments(.12f);
                    Assert.That(shell.Progress, Is.GreaterThan(progress), "Fragment motion follows real time independently of a stopped battle clock.");
                    scope.Until(() => current.HitsResolved == 4);
                    Assert.That(current.IsResolved, Is.True, "A break does not cut short the active skill's remaining hits.");
                    Assert.That((playerBroken ? duel.Enemy : duel.Player).Resistance, Is.EqualTo(9980),
                        "All four of the broken fighter's current hits actually dealt their 20 total resistance damage.");
                    Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(3), "Persistent broken status does not emit again on later hits.");
                    scope.Hud.BeginTurn();
                    Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.Zero);
                    Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.Zero);
                    scope.Hud.Reset();
                    Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.Zero);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothSidesBreak_CurrentFourHitsFinish_AndTheCancelledTailGoesStraightToAfterTurn()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                const int reservations = 12;
                LegacyQueuedDuel duel = scope.Prepare(new LegacyQueuedDuel(10000, 5, 10000, 5,
                    new[] { Attack(88211, 40) }, new[] { Attack(88212, 40) }, new[] { reservations }, meshPercent: 0));
                for (int i = 0; i < reservations; i++) Assert.That(scope.Controller.QueueLane(0), Is.True);
                scope.Begin();
                LegacyCurrentSlot current = duel.CurrentSlot;
                scope.Until(() => current.HitsResolved > 0);
                Assert.That(duel.PlayerBreakCanceledFromSlotIndex, Is.EqualTo(1));
                Assert.That(duel.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
                Assert.That(current.IsResolved, Is.False, "The current exchange still has three hits to play.");
                scope.Until(() => duel.CurrentSlot == null);
                Assert.That(current.HitsResolved, Is.EqualTo(4));
                Assert.That(duel.Player.Health, Is.EqualTo(9935));
                Assert.That(duel.Enemy.Health, Is.EqualTo(9935), "Both current skills dealt all four hits, and no cancelled attack dealt damage.");
                Assert.That(typeof(DuelPrototypeController).GetField("viewPhase", Private).GetValue(scope.Controller).ToString(),
                    Is.EqualTo("AfterTurn"), "No invisible idle skill or between-skill wait may play for the eleven cancelled pairs.");
                Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 1), Is.Null);
                Assert.That(scope.Hud.GetQueuedSkillAnchor(false, 1), Is.Null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualOpeningEffect_ShatterStartsBeforeTheFirstHit_AndResetClearsTheLivingFragments()
        {
            yield return null;
            using (var scope = new CampaignScope())
            {
                // 쿠페's real sheet effect breaks a defending opponent as the slot opens, before attack events.
                var coupe = Attack(42, 1, 1);
                var guard = new LegacySkill(88202, "검증 방어", 0, 100, 100, LegacySkillKind.Defence,
                    LegacySkillProperty.Defence, 1, 0, string.Empty, iconId: 1);
                LegacyQueuedDuel duel = scope.Prepare(new LegacyQueuedDuel(1000, 1000, 1000, 15,
                    new[] { coupe }, new[] { guard }, new[] { 4 }, meshPercent: 0));
                for (int i = 0; i < 4; i++) Assert.That(scope.Controller.QueueLane(0), Is.True);
                scope.Begin();
                Assert.That(duel.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(duel.Enemy.IsResistanceBroken, Is.True);
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.Zero,
                    "Canceled cards stay visible until the direct-effect body check connects.");
                scope.Until(() => scope.Controller.ArenaView.HasResistanceImpactContact);
                Assert.That(duel.CurrentSlot.HitsResolved, Is.Zero);
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.EqualTo(3));
                Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.EqualTo(3));
                AssertFragmentsOnSide(scope.Hud, false);
                scope.Hud.Reset();
                Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.Zero);
                Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualVersusGuest_ShowsTheBrokenHostsCancelledCardsOnTheOpponentsSide()
        {
            yield return null;
            using (var scope = new VersusScope())
            {
                LocalVersusMatch match = scope.Match;
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(match.TryQueueLane(0, 0), Is.True);
                }
                Assert.That(match.TryCommit(0), Is.True);
                for (int i = 0; i < 4; i++) Assert.That(match.TryQueueLane(1, 0), Is.True);
                Assert.That(match.TryCommit(1), Is.True);
                scope.Begin();
                LocalVersusSlot current = match.CurrentSlot;
                for (int frame = 0; frame < 1200 && current.HitsResolved == 0; frame++) scope.Versus.Tick(.01f, null);
                Assert.That(current.HitsResolved, Is.EqualTo(1));
                Assert.That(match.Left.State.IsResistanceBroken, Is.True);
                Assert.That(scope.Versus.Perspective.EnemyBreakCanceledFromSlotIndex, Is.EqualTo(1));
                Assert.That(scope.Versus.BattleHud.BreakFragmentEmissionCount, Is.EqualTo(3));
                Assert.That(scope.Versus.BattleHud.ActiveBreakFragmentCardCount, Is.EqualTo(3));
                AssertFragmentsOnSide(scope.Versus.BattleHud, false);
                Assert.That(scope.Versus.BattleHud.GetQueuedSkillAnchor(false, 0), Is.Not.Null);
                Assert.That(scope.Versus.BattleHud.GetQueuedSkillAnchor(false, 1), Is.Null);
                Assert.That(scope.Versus.BattleHud.GetQueuedSkillAnchor(true, 1), Is.Not.Null);
                scope.Versus.BattleHud.AdvanceBreakQueueFragments(1f);
                Assert.That(scope.Versus.BattleHud.ActiveBreakFragmentCardCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManyReservations_KeepAVisibleWindow_AndTheActualBreakCanBeRenderedAtThreeAspectRatios()
        {
            yield return null;
            using (var scope = new CampaignScope())
            using (var render = new BattleRender(scope.Controller))
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_BREAK_QUEUE_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                var renderedFragmentPixels = new List<int>();
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1280, 960), new Vector2Int(1920, 810) })
                {
                    render.Resize(size);
                    yield return null;
                    yield return null;
                    LegacyQueuedDuel duel = scope.Prepare(30, false);
                    for (int i = 0; i < 30; i++) Assert.That(scope.Controller.QueueLane(0), Is.True);
                    scope.Advance(0f);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(duel.PlayerQueue.Count, Is.EqualTo(30));
                    Assert.That(duel.EnemyQueue.Count, Is.EqualTo(30));
                    Assert.That(scope.Hud.QueueWindowCapacity, Is.GreaterThan(1).And.LessThan(30));
                    Assert.That(scope.Hud.QueueWindowStart, Is.GreaterThan(0), "Planning keeps the latest reservation in view.");
                    AssertVisibleInside(scope.Hud, true, 30, render.Camera, size);
                    AssertVisibleInside(scope.Hud, false, 30, render.Camera, size);
                    yield return null;
                    if (!string.IsNullOrEmpty(output)) render.Save(Path.Combine(output, size.x + "x" + size.y + "-30-reservations.png"));

                    scope.Begin();
                    scope.Until(() => duel.Enemy.IsResistanceBroken);
                    Assert.That(scope.Hud.BreakFragmentEmissionCount, Is.GreaterThan(0));
                    scope.Hud.AdvanceBreakQueueFragments(.12f);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    renderedFragmentPixels.Add(render.CaptureFragmentEvidence(scope.Hud, output, size.x + "x" + size.y));
                    Assert.That(scope.Hud.ActiveBreakFragmentCardCount, Is.GreaterThan(0));
                    Assert.That(scope.Hud.GetQueuedSkillAnchor(false, 1), Is.Null);
                    Assert.That(scope.Hud.GetQueuedSkillAnchor(true, 1), Is.Not.Null);
                }
                Assert.That(renderedFragmentPixels, Has.All.GreaterThan(100),
                    "Turning the fractured graphics off must change over 100 pixels at every viewport on the same frozen battle image.");
            }
            yield return null;
        }

        private static void AssertFragmentsOnSide(LegacyCombatHud hud, bool playerSide)
        {
            Transform layer = hud.Root.transform.Find("Queue Break Fragments");
            Assert.That(layer, Is.Not.Null);
            int count = 0;
            foreach (Transform child in layer)
            {
                if (!child.gameObject.activeSelf) continue;
                Assert.That(child.name, Is.EqualTo("Broken Queued Skill"));
                float x = ((RectTransform)child).anchoredPosition.x;
                Assert.That(playerSide ? -x : x, Is.GreaterThan(0f), "Fragments originate at the cancelled side's own cards.");
                count++;
            }
            Assert.That(count, Is.EqualTo(hud.ActiveBreakFragmentCardCount));
        }

        private static DuelQueueShatterGraphic FirstFragment(LegacyCombatHud hud)
        {
            DuelQueueShatterGraphic graphic = hud.Root.GetComponentInChildren<DuelQueueShatterGraphic>();
            Assert.That(graphic, Is.Not.Null);
            return graphic;
        }

        private static void AssertVisibleInside(LegacyCombatHud hud, bool player, int total, Camera camera, Vector2Int size)
        {
            int shown = 0;
            for (int i = 0; i < total; i++)
            {
                RectTransform card = hud.GetQueuedSkillAnchor(player, i);
                if (card == null) continue;
                var corners = new Vector3[4];
                card.GetWorldCorners(corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Assert.That(min.x, Is.GreaterThanOrEqualTo(-1f));
                Assert.That(max.x, Is.LessThanOrEqualTo(size.x + 1f));
                Assert.That(min.y, Is.GreaterThanOrEqualTo(-1f));
                Assert.That(max.y, Is.LessThanOrEqualTo(size.y + 1f));
                shown++;
            }
            Assert.That(shown, Is.InRange(1, hud.QueueWindowCapacity));
        }

        private static LegacySkill Attack(int id, int power, int hits = 4, int cost = 0)
            => new LegacySkill(id, "검증 연격", cost, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, iconId: 1);

        private sealed class CampaignScope : IDisposable
        {
            private readonly bool wasEnabled;
            private readonly LegacyQueuedDuel original;
            private readonly Action<float, Keyboard> advance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly FieldInfo arenaSettings;
            public DuelPrototypeController Controller { get; }
            public LegacyCombatHud Hud => Controller.Hud;
            public CampaignScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "The fixture must never write the player's save.");
                wasEnabled = Controller.enabled; original = Controller.Session; Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0}", settings);
                Set("presentationSettings", settings); arenaSettings.SetValue(Controller.ArenaView, settings);
                Set("mission", null); Set("guide", null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
            }
            public LegacyQueuedDuel Prepare(int count, bool playerBroken, int cost = 0)
                => Prepare(new LegacyQueuedDuel(10000, playerBroken ? 5 : 10000, 10000, playerBroken ? 10000 : 5,
                    new[] { Attack(88201, playerBroken ? 20 : 40, cost: cost) },
                    new[] { Attack(88202, playerBroken ? 40 : 20) }, new[] { count },
                    playerActGainBonus: 7, meshPercent: 0));
            public LegacyQueuedDuel Prepare(LegacyQueuedDuel duel)
            {
                Set("session", duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                Advance(0f);
                Assert.That(Controller.CanChoose, Is.True);
                return duel;
            }
            public void Begin()
            {
                Controller.Session.Commit();
                Hud.Refresh(Controller.Session, 10f, true, 0, null, null, null, 0f, 0f);
                Canvas.ForceUpdateCanvases();
                typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", Private).Invoke(Controller, null);
                Advance(0f);
            }
            public void Advance(float seconds) => advance(seconds, null);
            public void Until(Func<bool> done)
            {
                for (int frame = 0; frame < 1600 && !done(); frame++) Advance(.01f);
                Assert.That(done(), Is.True, "The actual presentation reached the expected combat step.");
            }
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                Set("presentationSettings", originalSettings); arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", original); Controller.RestartMatch(); Controller.enabled = wasEnabled;
                Object.Destroy(settings);
            }
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool wasEnabled;
            private readonly GameObject host;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Versus { get; }
            public LocalVersusMatch Match { get; }
            public VersusScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { wasEnabled = scene.enabled; scene.enabled = false; }
                host = new GameObject("Break Queue Versus Test"); art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0}", settings);
                arena = LegacyArenaView.Create(host.transform, art, settings);
                Versus = new LocalVersusController(host.transform, art, arena, () => { }, settings);
                LegacySkill[] left = { Attack(88231, 20) }, right = { Attack(88232, 40) };
                Versus.StartOnline(17, 1, false, 0, left, right, (action, lane) => { }, () => { });
                Match = new LocalVersusMatch(left, right, 10000, 5, 10000, 10000, randomSeed: 17);
                typeof(LocalVersusController).GetField("match", Private).SetValue(Versus, Match);
                typeof(LocalVersusController).GetField("view", Private).SetValue(Versus, new LocalVersusPerspective(Match, 1));
                Versus.BattleHud.Reset();
            }
            public void Begin()
            {
                Versus.BattleHud.Refresh(Versus.Perspective, 10f, true, 0, null, null, null, 0f, 0f);
                Canvas.ForceUpdateCanvases();
                typeof(LocalVersusController).GetMethod("BeginSlot", Private).Invoke(Versus, null);
            }
            public void Dispose()
            {
                Versus.Dispose(); arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(host);
                if (scene != null) scene.enabled = wasEnabled;
            }
        }

        // Render the real battle and its native canvas into a chosen viewport; the player's editor view is never resized.
        private sealed class BattleRender : IDisposable
        {
            private readonly Canvas canvas;
            private readonly CanvasScaler scaler;
            private readonly RenderMode mode;
            private readonly Camera oldWorldCamera;
            private readonly CanvasScaler.ScaleMode scaleMode;
            private readonly float oldScale, plane, aspect;
            private readonly int culling;
            private readonly RenderTexture oldTarget;
            private readonly Dictionary<GameObject, int> layers = new Dictionary<GameObject, int>();
            private RenderTexture target;
            public Camera Camera { get; }
            public BattleRender(DuelPrototypeController controller)
            {
                canvas = controller.Hud.Root.GetComponent<Canvas>(); scaler = canvas.GetComponent<CanvasScaler>();
                Camera = controller.ArenaView.ArenaCamera;
                mode = canvas.renderMode; oldWorldCamera = canvas.worldCamera; plane = canvas.planeDistance;
                scaleMode = scaler.uiScaleMode; oldScale = scaler.scaleFactor;
                oldTarget = Camera.targetTexture; culling = Camera.cullingMask; aspect = Camera.aspect;
                foreach (Transform node in controller.Hud.Root.GetComponentsInChildren<Transform>(true))
                { layers[node.gameObject] = node.gameObject.layer; node.gameObject.layer = 31; }
                Camera.cullingMask |= 1 << 31;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera;
                canvas.planeDistance = Mathf.Max(1f, Camera.nearClipPlane + .1f);
            }
            public void Resize(Vector2Int size)
            {
                Camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Camera.targetTexture = target; Camera.aspect = size.x / (float)size.y;
                float logX = Mathf.Log(size.x / scaler.referenceResolution.x, 2f);
                float logY = Mathf.Log(size.y / scaler.referenceResolution.y, 2f);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logX, logY, scaler.matchWidthOrHeight));
            }
            public void Save(string path)
                => Capture(path);

            private Color32[] Capture(string path = null)
            {
                // Queue cards and pooled fragments may have been allocated after this render helper was created.
                foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (!layers.ContainsKey(node.gameObject)) layers[node.gameObject] = node.gameObject.layer;
                    node.gameObject.layer = 31;
                }
                Canvas.ForceUpdateCanvases();
                Camera.Render();
                RenderTexture old = RenderTexture.active;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, texture.EncodeToPNG());
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }

            public int CaptureFragmentEvidence(LegacyCombatHud hud, string output, string name)
            {
                Transform layer = hud.Root.transform.Find("Queue Break Fragments");
                Assert.That(layer, Is.Not.Null);
                bool wasActive = layer.gameObject.activeSelf;
                Color32[] visible = Capture(string.IsNullOrEmpty(output) ? null : Path.Combine(output, name + "-broken-queue.png"));
                var nodes = new List<FragmentNodeEvidence>();
                foreach (DuelQueueShatterGraphic graphic in layer.GetComponentsInChildren<DuelQueueShatterGraphic>())
                {
                        Mesh mesh = graphic.canvasRenderer.GetMesh();
                        int vertexCount = mesh == null ? 0 : mesh.vertexCount;
                        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                        foreach (Vector3 vertex in mesh == null ? Array.Empty<Vector3>() : mesh.vertices)
                        {
                            Vector2 screen = RectTransformUtility.WorldToScreenPoint(Camera, graphic.transform.TransformPoint(vertex));
                            min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                        }
                        nodes.Add(new FragmentNodeEvidence
                        {
                            name = graphic.transform.parent.name + "/" + graphic.name,
                            active = graphic.gameObject.activeInHierarchy,
                            enabled = graphic.enabled,
                            parentLocalPosition = graphic.transform.parent.localPosition,
                            worldPosition = graphic.transform.position,
                            localScale = graphic.transform.parent.localScale,
                            lossyScale = graphic.transform.lossyScale,
                            progress = graphic.Progress,
                            vertexCount = vertexCount,
                            meshScreenMin = vertexCount == 0 ? Vector2.zero : min,
                            meshScreenMax = vertexCount == 0 ? Vector2.zero : max,
                            rendererCulled = graphic.canvasRenderer.cull,
                            rendererAlpha = graphic.canvasRenderer.GetAlpha(),
                            color = graphic.color,
                            depth = graphic.depth,
                            canvasName = graphic.canvas == null ? null : graphic.canvas.name,
                            textureName = graphic.mainTexture == null ? null : graphic.mainTexture.name,
                            materialCount = graphic.canvasRenderer.materialCount
                        });
                }
                Color32[] hidden;
                try
                {
                    layer.gameObject.SetActive(false);
                    hidden = Capture(string.IsNullOrEmpty(output) ? null : Path.Combine(output, name + "-without-fragments.png"));
                }
                finally { layer.gameObject.SetActive(wasActive); Canvas.ForceUpdateCanvases(); }
                int changed = 0, maximumDifference = 0;
                for (int i = 0; i < visible.Length; i++)
                {
                    int difference = Math.Abs(visible[i].r - hidden[i].r) + Math.Abs(visible[i].g - hidden[i].g) +
                        Math.Abs(visible[i].b - hidden[i].b);
                    if (difference > 12) changed++;
                    maximumDifference = Math.Max(maximumDifference, difference);
                }
                var evidence = new FragmentRenderEvidence
                {
                    width = target.width, height = target.height,
                    changedPixels = changed, maximumRgbDifference = maximumDifference,
                    activeFragmentCards = hud.ActiveBreakFragmentCardCount,
                    emissionCount = hud.BreakFragmentEmissionCount,
                    layerActive = wasActive, layerLocalPosition = layer.localPosition,
                    layerWorldPosition = layer.position, layerLossyScale = layer.lossyScale,
                    nodes = nodes.ToArray()
                };
                if (!string.IsNullOrEmpty(output))
                    File.WriteAllText(Path.Combine(output, name + "-fragment-render-evidence.json"), JsonUtility.ToJson(evidence, true));
                return changed;
            }

            [Serializable]
            private sealed class FragmentRenderEvidence
            {
                public int width, height, changedPixels, maximumRgbDifference, activeFragmentCards, emissionCount;
                public bool layerActive;
                public Vector3 layerLocalPosition, layerWorldPosition, layerLossyScale;
                public FragmentNodeEvidence[] nodes;
            }

            [Serializable]
            private sealed class FragmentNodeEvidence
            {
                public string name, canvasName, textureName;
                public bool active, enabled, rendererCulled;
                public Vector3 parentLocalPosition, worldPosition, localScale, lossyScale;
                public Vector2 meshScreenMin, meshScreenMax;
                public Color color;
                public float progress, rendererAlpha;
                public int vertexCount, depth, materialCount;
            }
            public void Dispose()
            {
                Camera.targetTexture = oldTarget; Camera.cullingMask = culling; Camera.aspect = aspect;
                canvas.renderMode = mode; canvas.worldCamera = oldWorldCamera; canvas.planeDistance = plane;
                scaler.uiScaleMode = scaleMode; scaler.scaleFactor = oldScale;
                foreach (KeyValuePair<GameObject, int> layer in layers) if (layer.Key != null) layer.Key.layer = layer.Value;
                if (target != null) { target.Release(); Object.Destroy(target); }
            }
        }
    }
}
