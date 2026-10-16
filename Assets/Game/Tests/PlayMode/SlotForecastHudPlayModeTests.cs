using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Reading the skills apart on screen (Docs/SlotForecast.md, Docs/SkillInformationDesign.md): 편성 예상's marks on the
    /// player's queued cards and its line in the held explanation and the Tab card, read from the duel's own forecast and
    /// switched by the settings (versus on its own); every skill's school dye on its window, card and slot, and its core
    /// mechanic's pip wherever a skill is picked.</summary>
    public sealed class SlotForecastHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator QueuedCards_CarryTheirSlotsVerdict_WhilePlanning_AndNothingWhileTheTurnResolves()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel();
                fixture.Refresh(duel);
                Assert.That(hud.GetForecastVerdict(0), Is.Null, "Nothing queued, nothing marked.");
                Assert.That(duel.TryQueueLane(0), Is.True, "베기 against the enemy's 깊은 찌르기…");
                Assert.That(duel.TryQueueLane(1), Is.True, "…깊은 찌르기 against its 막기.");
                fixture.Refresh(duel);
                IReadOnlyList<LegacySlotForecast> forecast = duel.ForecastTurn();
                Assert.That(hud.GetForecastVerdict(0), Is.EqualTo(LegacyForecastVerdict.Unfavourable), forecast[0].Summary);
                Assert.That(hud.GetForecastVerdict(1), Is.EqualTo(LegacyForecastVerdict.Favourable), forecast[1].Summary);
                for (int slot = 0; slot < 2; slot++)
                {
                    Assert.That(hud.GetForecastVerdict(slot), Is.EqualTo(hud.Judge(forecast[slot])), "The duel's own forecast, slot " + slot);
                    RectTransform mark = hud.GetForecastMark(slot), card = hud.GetQueuedSkillAnchor(true, slot);
                    Assert.That(mark.parent, Is.SameAs(card), "The mark rides its card…");
                    Assert.That(mark.position.x, Is.LessThan(card.position.x), "…on its left rim.");
                    Assert.That(mark.GetComponent<Image>().color, Is.EqualTo(LegacyCombatHud.ForecastFill(hud.GetForecastVerdict(slot).Value)));
                    Assert.That(mark.GetComponent<Image>().raycastTarget, Is.False);
                }
                foreach (Transform node in hud.Root.transform.Find("Enemy Requests").GetComponentsInChildren<Transform>(true))
                    Assert.That(node.name, Is.Not.EqualTo("Forecast Mark"), "Only the player's cards are marked.");
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(2), "Showing the forecast queues nothing…");
                Assert.That(duel.Act, Is.EqualTo(1), "…and spends nothing.");

                duel.Commit();
                duel.BeginNextSlot();
                hud.Refresh(duel, 10f, true, 0, null, null, null);
                Assert.That(hud.GetForecastVerdict(0) ?? hud.GetForecastVerdict(1), Is.Null, "The marks are for planning only.");
            }
        }

        [UnityTest]
        public IEnumerator HeldExplanation_TellsTheSlotItWouldTake_AndTab_TheInspectedSlots()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel();
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(1)[0], false);
                Assert.That(hud.ForecastNote, Is.EqualTo(duel.ForecastIfQueued(1).Summary), "깊은 찌르기, queued now, would meet 깊은 찌르기.");
                Assert.That(hud.ForecastNote, Does.StartWith(LegacySlotForecast.SummaryPrefix));
                RectTransform panel = (RectTransform)hud.Root.transform.Find("Skill Explain");
                RectTransform note = (RectTransform)panel.Find("Forecast Note"), hint = (RectTransform)panel.Find("Explanation Hint");
                Canvas.ForceUpdateCanvases();
                Assert.That(ScreenRect(note).Overlaps(ScreenRect(hint)), Is.False, "Its row is clear of the hint…");
                Assert.That(ScreenRect(note).Overlaps(ScreenRect((RectTransform)panel.Find("Mesh Note"))), Is.False, "…of 맞물림's…");
                Assert.That(ScreenRect(note).Overlaps(ScreenRect((RectTransform)panel.Find("Skill Summary"))), Is.False, "…and of the card.");

                Assert.That(duel.TryQueueLane(1), Is.True);
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(2)[0], false);
                Assert.That(hud.ForecastNote, Is.EqualTo(duel.ForecastIfQueued(2).Summary), "It follows the queue: 기세 꺾기 in slot 2.");
                Assert.That(duel.TryQueueLane(2) && duel.TryQueueLane(0), Is.True);
                fixture.Refresh(duel);
                hud.ShowExplanation(duel.GetLane(1)[0], false);
                Assert.That(hud.ForecastNote, Is.EqualTo(LegacyCombatHud.ForecastUnavailable), "정교한 찌르기 is out of reach of 0 ACT.");
                hud.HideExplanation();

                hud.SetInspectedSlot(1);
                hud.ShowExplanation(duel.EnemyQueue[1], true);
                Assert.That(hud.EnemyForecastNote, Is.EqualTo(duel.ForecastTurn()[1].Summary), "Tab reads the inspected slot.");
                hud.SetInspectedSlot(0);
                hud.ShowExplanation(duel.EnemyQueue[0], true);
                Assert.That(hud.EnemyForecastNote, Is.EqualTo(duel.ForecastTurn()[0].Summary));
                hud.HideExplanation();
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(3), "Reading never queues.");
            }
        }

        [UnityTest]
        public IEnumerator Settings_SwitchTheForecastOff_AndVersusOnItsOwn()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                Assert.That(settings.SlotForecastEnabled && settings.SlotForecastInVersus, Is.True, "On by default.");
                Assert.That(settings.SlotForecastResistanceWeight, Is.EqualTo(LegacySlotForecast.DefaultResistanceWeightPercent));
                Assert.That(settings.SlotForecastBreakBonus, Is.EqualTo(LegacySlotForecast.DefaultBreakBonus));
                Assert.That(settings.SlotForecastMargin, Is.EqualTo(LegacySlotForecast.DefaultMargin));
                using (var fixture = new HudFixture(settings))
                {
                    LegacyQueuedDuel duel = Duel();
                    Assert.That(duel.TryQueueLane(0), Is.True);
                    fixture.Refresh(duel);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Not.Null);

                    Set(settings, "slotForecastMargin", 30);
                    fixture.Refresh(duel);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.EqualTo(LegacyForecastVerdict.Even), "A wide margin reads it as close.");

                    Set(settings, "slotForecastEnabled", false);
                    fixture.Refresh(duel);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Null, "Switched off: no mark…");
                    fixture.Hud.ShowExplanation(duel.GetLane(1)[0], false);
                    Assert.That(fixture.Hud.ForecastNote, Is.Null, "…and no line.");
                    fixture.Hud.HideExplanation();
                    Set(settings, "slotForecastEnabled", true);

                    var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 3);
                    Assert.That(match.TryQueueLane(0, 1) && match.TryQueueLane(1, 0), Is.True);
                    var view = new LocalVersusPerspective(match, 0);
                    fixture.Hud.Reset();
                    fixture.Hud.Refresh(view, 10f, false, -1, null, null, null);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Not.Null, "Versus forecasts through its perspective…");
                    var forecast = new List<LegacySlotForecast>();
                    view.ForecastTurn(forecast);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.EqualTo(fixture.Hud.Judge(forecast[0])));
                    Set(settings, "slotForecastInVersus", false);
                    fixture.Hud.Refresh(view, 10f, false, -1, null, null, null);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Null, "…unless switched off there.");
                    fixture.Hud.Reset();
                    fixture.Refresh(duel);
                    Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Not.Null, "Single play keeps it.");
                }
            }
            finally { Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator BattleWindowsAndCards_WearTheirSchoolsDye_AndTheWindowsTheirCoreMechanic()
        {
            yield return null;
            var dyes = new HashSet<Color>();
            for (int lane = 0; lane < 3; lane++)
            {
                Color edge = SkillLaneStyle.Edge(lane);
                Assert.That(edge.a, Is.EqualTo(1f));
                Assert.That(dyes.Add(edge), Is.True, "Three distinct dyes.");
                Color.RGBToHSV(edge, out float hue, out float saturation, out _);
                Color.RGBToHSV(SkillLaneStyle.Paper(lane), out float paperHue, out float paperSaturation, out _);
                Assert.That(hue, Is.EqualTo(paperHue).Within(.01f), "The paper's own hue…");
                Assert.That(saturation, Is.GreaterThan(paperSaturation), "…deepened for the dark HUD.");
            }
            using (var fixture = new HudFixture())
            {
                LegacyQueuedDuel duel = Duel();
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(1), Is.True);
                Assert.That(duel.TryQueueBreath(), Is.True);
                fixture.Refresh(duel);
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = SkillLaneStyle.Key(lane);
                    Transform window = fixture.Hud.Root.transform.Find("Input/Keys/Current " + key);
                    Assert.That(window.GetComponent<Image>().color, Is.EqualTo(SkillLaneStyle.Edge(lane)), key + "'s ring.");
                    Assert.That(window.Find("Style Name").GetComponent<Text>().color, Is.EqualTo(SkillLaneStyle.Edge(lane)));
                    AssertPip(window.Find("Role Pip"), duel.GetLane(lane)[0]);
                }
                Transform player = fixture.Hud.Root.transform.Find("Player Requests");
                Assert.That(player.GetChild(0).GetComponent<Image>().color, Is.EqualTo(SkillLaneStyle.Edge(0)));
                Assert.That(player.GetChild(1).GetComponent<Image>().color, Is.EqualTo(SkillLaneStyle.Edge(1)));
                Assert.That(player.GetChild(2).GetComponent<Image>().color, Is.EqualTo(DuelVisualTheme.Border), "숨고르기 has no school.");
                // The card itself: the enemy row's first child is the guide's focus frame, not a card.
                Image EnemyCard() => fixture.Hud.GetQueuedSkillAnchor(false, 0).GetComponent<Image>();
                Assert.That(EnemyCard().color, Is.EqualTo(SkillLaneStyle.Edge(duel.EnemyQueue[0].LaneIndex)),
                    "With all three schools open the enemy's cards are dyed too…");

                fixture.Hud.Reset();
                var qOnly = new LegacyQueuedDuel(100, 50, 100, 30, LegacyInitialSkills.All,
                    new EnemyScript(new[] { new[] { LegacySkillDefinitions.Skill(3) } }), 2, features: CombatFeature.LaneQ);
                fixture.Refresh(qOnly);
                Assert.That(EnemyCard().color, Is.EqualTo(DuelVisualTheme.Border),
                    "…but not before, as its Tab badge stays neutral.");
            }
        }

        [UnityTest]
        public IEnumerator LoadoutCurriculumAndVersusCards_ShowEachSkillsCoreMechanic()
        {
            yield return null;
            var parent = new GameObject("Skill Role Pip Lobby Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run); lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                // A gear socket shows only its icon and ACT (2026-10-16): the core mark rides the owned cards and the detail.
                for (int lane = 0; lane < 3; lane++)
                    for (int slot = 0; slot < 3; slot++)
                    {
                        Transform socket = Named(lobby.Root.transform, "Loadout Slot " + SkillLaneStyle.Key(lane) + " " + (slot + 1));
                        Assert.That(socket.Find("Role Pip"), Is.Null, "No core mark on a socket…");
                        Assert.That(socket.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(1), "…its ACT the only text.");
                        Assert.That(socket.Find("Slot ACT").GetComponent<Text>().text, Is.EqualTo("ACT " + run.GetLoadoutSlot(lane, slot).Skill.Cost));
                    }
                CampaignOwnedSkill freed = run.GetLoadoutSlot(1, 0);
                Assert.That(run.TryUnequipSkill(freed.SkillId), Is.True);
                lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                AssertPip(Named(lobby.Root.transform, "Loadout Owned Skill " + freed.SkillId).Find("Role Pip"), freed.Skill);
                Assert.That(run.TryResetLoadout(), Is.True);

                lobby.ShowTab(LobbyTab.Curriculum);
                yield return null;
                foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                {
                    if (node.SkillIds.Count == 0) continue;
                    LegacySkill skill = LegacySkillDefinitions.Skill(node.SkillIds[0]);
                    Transform card = Named(lobby.Root.transform, "Curriculum Node " + node.Id);
                    CurriculumNodeState state = run.Curriculum.GetState(node.Id);
                    AssertPip(card.Find("Role Pip"), skill,
                        state == CurriculumNodeState.Locked || state == CurriculumNodeState.Excluded ? .4f : 1f);
                    Assert.That(card.Find("Node Lane").GetComponent<Text>().color, Is.EqualTo(SkillLaneStyle.Edge(skill.LaneIndex)));
                }
            }
            finally { lobby?.Dispose(); Object.Destroy(parent); }

            var host = new GameObject("Skill Role Pip Versus Test");
            try
            {
                var loadout = new VersusLoadout();
                using (var art = new LegacyDuelArt())
                using (var hud = new VersusLoadoutHud(host.transform, art, loadout))
                {
                    hud.Show();
                    // As the campaign's gears (2026-10-16): a socket shows only its icon and ACT; the core mark rides the
                    // pool's cards and the detail.
                    for (int lane = 0; lane < 3; lane++)
                    {
                        string key = SkillLaneStyle.Key(lane);
                        Assert.That(Named(hud.Root.transform, "Versus Loadout Heading " + key).GetComponent<Text>().text,
                            Is.EqualTo(SkillLaneStyle.FullName(lane)), "The school's badge, as the campaign's.");
                        for (int slot = 0; slot < 3; slot++)
                        {
                            Transform socket = Named(hud.Root.transform, "Versus Slot " + key + " " + (slot + 1));
                            Assert.That(socket.Find("Role Pip"), Is.Null, "No core mark on a socket…");
                            Assert.That(socket.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(1), "…its ACT the only text.");
                            Assert.That(socket.Find("Slot ACT").GetComponent<Text>().text, Is.EqualTo("ACT " + loadout.GetSkill(lane, slot).Cost));
                        }
                    }
                    foreach (LegacySkill skill in LegacySkillDefinitions.InitialSkills)
                        Assert.That(Named(hud.Root.transform, "Versus Available Skill " + skill.Id).gameObject.activeSelf, Is.False,
                            skill.Name + " sits in a socket, so out of the rack.");
                    foreach (LegacySkill skill in LegacySkillDefinitions.AcquisitionSkills)
                    {
                        // 에스키브 (54) is kept out of the versus pool (VersusLoadout.ExcludedSkillIds): no card at all.
                        if (VersusLoadout.IsExcluded(skill.Id))
                        {
                            Assert.That(Array.Find(hud.Root.GetComponentsInChildren<Transform>(true),
                                node => node.name == "Versus Available Skill " + skill.Id), Is.Null, skill.Name);
                            continue;
                        }
                        Transform card = Named(hud.Root.transform, "Versus Available Skill " + skill.Id);
                        AssertPip(card.Find("Role Pip"), skill);
                        Assert.That(card.Find("Available Lane").GetComponent<Text>().color, Is.EqualTo(SkillLaneStyle.Edge(skill.LaneIndex)));
                    }
                    LegacySkill picked = LegacySkillDefinitions.AcquisitionSkills[0];
                    Named(hud.Root.transform, "Versus Available Skill " + picked.Id).GetComponent<Button>().onClick.Invoke();
                    Assert.That(Named(hud.Root.transform, "Versus Detail Heading").GetComponent<Text>().text,
                        Is.EqualTo(SkillLaneStyle.FullName(picked.LaneIndex)));
                    Assert.That(Named(hud.Root.transform, "Versus Detail Name").GetComponent<Text>().text, Is.EqualTo(picked.Name));
                    Assert.That(Array.Find(hud.Root.GetComponentsInChildren<Transform>(true), node => node.name == "Versus Detail Kind"),
                        Is.Null, "No role line under the name, as the campaign loadout's (2026-10-16).");
                    Assert.That(Named(hud.Root.transform, "Versus Detail Name").GetComponent<RectTransform>().anchoredPosition.y,
                        Is.EqualTo(Named(hud.Root.transform, "Versus Detail Icon").GetComponent<RectTransform>().anchoredPosition.y)
                            .Within(.01f), "The name sits level with the icon, as the campaign loadout's.");
                }
            }
            finally { Object.Destroy(host); }
        }

        /// <param name="alpha">The pip's dimming: 1, or .4 on a locked or excluded curriculum node.</param>
        private static void AssertPip(Transform pip, LegacySkill skill, float alpha = 1f)
        {
            Assert.That(pip, Is.Not.Null, "Missing role pip for " + skill.Name);
            Assert.That(pip.gameObject.activeSelf, Is.True, skill.Name);
            SkillInfoContent content = SkillInfoContent.For(skill, false);
            Color paper = SkillToneStyle.Paper(content.MainTone), ink = SkillToneStyle.Ink(content.MainTone);
            paper.a = ink.a = alpha;
            Assert.That(pip.GetComponent<Image>().color, Is.EqualTo(paper), skill.Name + "'s first keyword's dye.");
            Assert.That(pip.GetComponent<Image>().raycastTarget, Is.False);
            SkillInfoGlyph glyph = pip.GetComponentInChildren<SkillInfoGlyph>(true);
            Assert.That(glyph.color, Is.EqualTo(ink), skill.Name + "'s glyph.");
            Assert.That(glyph.raycastTarget, Is.False);
        }

        // Every lane open, 4 ACT: the enemy queues 깊은 찌르기, 막기, 기세 꺾기.
        private static LegacyQueuedDuel Duel()
            => new LegacyQueuedDuel(100, 50, 100, 30, LegacyInitialSkills.All,
                new EnemyScript(new[] { new[] { LegacySkillDefinitions.Skill(3), LegacySkillDefinitions.Skill(7), LegacySkillDefinitions.Skill(5) } }),
                2, playerActGainBonus: 1);

        private static void Set(DuelPresentationSettings settings, string field, object value)
            => typeof(DuelPresentationSettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, value);

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Transform Named(Transform root, string name)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                if (node.name == name) return node;
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent;
            public LegacyCombatHud Hud { get; }

            public HudFixture(DuelPresentationSettings settings = null)
            {
                parent = new GameObject("Slot Forecast HUD Test");
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null, settings);
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(LegacyQueuedDuel duel)
            {
                Hud.Refresh(duel, 10f, false, -1, null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
            }
        }
    }
}
