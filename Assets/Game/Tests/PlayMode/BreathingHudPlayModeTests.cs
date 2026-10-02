using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BreathingHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator Clicks_QueueOneIdleEach_KeepAct_AndDisableAfterThird()
        {
            using (var fixture = new HudFixture())
            {
                fixture.Refresh();
                yield return null;
                int act = fixture.Session.Act;
                Assert.That(fixture.Label("Label").text, Is.EqualTo("숨고르기"));
                Assert.That(fixture.Label("ACT Cost").text, Is.EqualTo("ACT 0"));
                Assert.That(fixture.Label("KeyHint").text, Is.EqualTo("S"));
                Assert.That(fixture.Label("Remaining").text, Is.EqualTo("남은 3/3"));
                for (int i = 0; i < 3; i++)
                {
                    ClickEmblem(fixture.Button);
                    Assert.That(fixture.CallbackCount, Is.EqualTo(i + 1));
                    Assert.That(fixture.Session.PlayerQueue.Count, Is.EqualTo(i + 1));
                    Assert.That(fixture.Session.PlayerQueue[i], Is.SameAs(LegacyCommonActions.Breathe));
                    Assert.That(fixture.Session.Act, Is.EqualTo(act));
                    Assert.That(fixture.Label("Remaining").text, Is.EqualTo($"남은 {2 - i}/3"));
                }
                Assert.That(fixture.Button.interactable, Is.False);
                // Direct UnityEvent invocation must also reject a stale fourth click.
                fixture.Button.onClick.Invoke();
                Assert.That(fixture.CallbackCount, Is.EqualTo(3));
                Assert.That(fixture.Session.PlayerQueue.Count, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator PlanningWithZeroAct_StillAllowsBreathing_AndAuthoritativeChangesRefreshCount()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                Assert.That(fixture.Session.TryQueueLane(0), Is.True);
                Assert.That(fixture.Session.TryQueueLane(0), Is.True);
                Assert.That(fixture.Session.Act, Is.Zero);
                fixture.Refresh();
                Assert.That(fixture.Button.interactable, Is.True);
                Assert.That(fixture.Session.TryQueueBreath(), Is.True);
                Assert.That(fixture.Session.TryQueueBreath(), Is.True);
                fixture.Refresh();
                Assert.That(fixture.Label("Remaining").text, Is.EqualTo("남은 1/3"));
                fixture.Button.onClick.Invoke();
                Assert.That(fixture.CallbackCount, Is.EqualTo(1));
                Assert.That(fixture.Session.Act, Is.Zero);
                Assert.That(fixture.Label("Remaining").text, Is.EqualTo("남은 0/3"));
                fixture.Session.Reset();
                fixture.Hud.Reset();
                Assert.That(fixture.Button.interactable, Is.False, "Reset waits for an authoritative planning refresh.");
                fixture.Refresh();
                Assert.That(fixture.Button.interactable, Is.True);
                Assert.That(fixture.Label("Remaining").text, Is.EqualTo("남은 3/3"));
            }
        }

        [UnityTest]
        public IEnumerator MissionInspectionEndTurnAndCombat_BlockTheCommonAction()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                fixture.Refresh();
                fixture.Hud.SetMissionMode(true);
                Assert.That(fixture.Button.gameObject.activeSelf, Is.False);
                fixture.Button.onClick.Invoke();
                fixture.Hud.SetMissionMode(false);
                Assert.That(fixture.Button.gameObject.activeSelf, Is.True);
                Assert.That(fixture.Button.interactable, Is.True);
                fixture.Hud.SetInspectedSlot(0);
                Assert.That(fixture.Button.interactable, Is.False);
                fixture.Button.onClick.Invoke();
                fixture.Hud.SetInspectedSlot(-1);
                Assert.That(fixture.Button.interactable, Is.True);
                fixture.Hud.EndTurn();
                Assert.That(fixture.Button.interactable, Is.False);
                fixture.Button.onClick.Invoke();
                fixture.Session.Commit();
                fixture.Refresh(true);
                Assert.That(fixture.Button.interactable, Is.False);
                fixture.Button.onClick.Invoke();
                Assert.That(fixture.CallbackCount, Is.Zero);
                Assert.That(fixture.Session.PlayerQueue, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator MissingCallback_DisablesButtonWithoutChangingExistingConstructorCallers()
        {
            yield return null;
            using (var fixture = new HudFixture(false))
            {
                fixture.Refresh();
                Assert.That(fixture.Button.gameObject.activeSelf, Is.True);
                Assert.That(fixture.Button.interactable, Is.False);
                fixture.Button.onClick.Invoke();
                Assert.That(fixture.Session.PlayerQueue, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator IdleQueueCardAndCombatLog_UseThePauseEmblem_WithoutWeaponExplanation()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                Assert.That(fixture.Session.TryQueueBreath(), Is.True);
                Assert.That(fixture.Session.TryQueueLane(0), Is.True);
                fixture.Refresh();
                RectTransform card = fixture.Hud.GetQueuedSkillAnchor(true, 0);
                Sprite emblem = fixture.Art.GetSkillIcon(LegacyCommonActions.Breathe.IconId);
                Assert.That(card, Is.Not.Null);
                Assert.That(card.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(emblem));
                Assert.That(fixture.Hud.GetQueuedSkillAnchor(true, 1), Is.Not.Null);
                fixture.Hud.ShowExplanation(LegacySkillDefinitions.Skill(1), false);
                Assert.That(fixture.Hud.Root.transform.Find("Skill Explain").gameObject.activeSelf, Is.True);
                fixture.Hud.ShowExplanation(LegacyCommonActions.Breathe, false);
                Assert.That(fixture.Hud.Root.transform.Find("Skill Explain").gameObject.activeSelf, Is.False,
                    "An idle action must not inherit a Q/W/E badge or attack-type explanation.");
                fixture.Session.Commit();
                fixture.Hud.SetCurrentSkills(LegacyCommonActions.Breathe, fixture.Session.EnemyQueue[0], .4f);
                fixture.Refresh(true, 0, .25f);
                Assert.That(fixture.Hud.GetQueuedSkillAnchor(true, 0), Is.SameAs(card));
                Assert.That(card.localScale.x, Is.InRange(1f, 1.2001f));
                fixture.Hud.RecordResolvedSlot(LegacyCommonActions.Breathe, null, 0, 0);
                Transform row = fixture.Hud.Root.transform.Find("Log View/Log Scroll View/Viewport/Content/Player/Log Player Skill");
                Assert.That(row, Is.Not.Null);
                Assert.That(row.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(emblem));
                Assert.That(row.Find("Skill").GetComponent<Text>().text, Is.EqualTo("숨고르기"));
                fixture.Refresh(true, 1);
                Assert.That(fixture.Hud.GetQueuedSkillAnchor(true, 0), Is.Null);
                Assert.That(fixture.Hud.GetQueuedSkillAnchor(true, 1), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator GeneratedEmblem_IsCachedAndOwnedByArt_NotByTheHud()
        {
            yield return null;
            var art = new LegacyDuelArt();
            Sprite sword = art.GetSkillIcon(1);
            Sprite emblem = art.GetSkillIcon(LegacyCommonActions.Breathe.IconId);
            Texture2D texture = emblem.texture;
            Assert.That(emblem, Is.SameAs(art.GetSkillIcon(LegacyCommonActions.Breathe.IconId)));
            Assert.That(emblem, Is.Not.SameAs(sword));
            Assert.That(emblem.rect.size, Is.EqualTo(Vector2.one * 64));
            var parent = new GameObject("Shared Art Disposal Fixture");
            using (var hud = new LegacyCombatHud(parent.transform, art, null, null, null))
                Assert.That(hud.Root, Is.Not.Null);
            Object.Destroy(parent);
            yield return null;
            Assert.That(emblem, Is.Not.Null, "The HUD must not destroy the Art-owned emblem used by other consumers.");
            art.Dispose();
            art.Dispose();
            yield return null;
            Assert.That(emblem == null, Is.True);
            Assert.That(texture == null, Is.True);
            Assert.That(sword, Is.Not.Null, "Disposal must never destroy imported shared resources.");
            Assert.That(art.GetSkillIcon(LegacyCommonActions.Breathe.IconId), Is.Null);
        }

        private static void ClickEmblem(Button button)
        {
            Assert.That(button.IsInteractable(), Is.True);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.Find("Icon").position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject owner = new GameObject("Breathing HUD Fixture");
            private readonly Camera camera;
            private readonly Transform player, enemy;
            public readonly LegacyDuelArt Art = new LegacyDuelArt();
            public readonly LegacyQueuedDuel Session = new LegacyQueuedDuel();
            public readonly LegacyCombatHud Hud;
            public int CallbackCount { get; private set; }
            public Button Button => Hud.Root.transform.Find("Input/Keys/BreathButton").GetComponent<Button>();
            public Text Label(string name) => Button.transform.Find(name).GetComponent<Text>();

            public HudFixture(bool enableCallback = true)
            {
                camera = new GameObject("Fixture Camera").AddComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.transform.localPosition = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 6;
                camera.enabled = false;
                player = new GameObject("Fixture Player").transform;
                enemy = new GameObject("Fixture Enemy").transform;
                player.SetParent(owner.transform, false);
                enemy.SetParent(owner.transform, false);
                player.localPosition = Vector3.left * 2;
                enemy.localPosition = Vector3.right * 2;
                Action callback = enableCallback ? (Action)(() => { CallbackCount++; Session.TryQueueBreath(); }) : null;
                Hud = new LegacyCombatHud(owner.transform, Art, null, null, null, null, callback);
                Hud.Root.GetComponent<Canvas>().sortingOrder = 500;
            }

            public void Refresh(bool resolving = false, int slot = -1, float delta = 0)
            {
                Hud.Refresh(Session, 10, resolving, slot, camera, player, enemy, delta, delta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Art.Dispose();
                Object.Destroy(owner);
            }
        }
    }
}
