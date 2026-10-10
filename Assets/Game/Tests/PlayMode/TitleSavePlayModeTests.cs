using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class TitleSavePlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator Title_WithoutSave_OffersOnlyNewGame_AndNewGameStartsTheArcAndSaves()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.TitleHud.IsVisible, Is.True);
                Assert.That(controller.IsInLobby, Is.False);
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(controller.AutoSaveEnabled, Is.False, "Nothing is written before the player chooses.");
                Assert.That(controller.StoryProgressionEnabled, Is.False);
                Assert.That(controller.TitleHud.CanContinue, Is.False);
                Assert.That(controller.TitleHud.ContinueButton.interactable, Is.False);
                Assert.That(Label(controller.TitleHud.Root, "Title Save Summary").text, Does.Contain("없습니다"));
                Assert.That(controller.TitleHud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(TitleHud.SortingOrder));
                Image titleRoom = NamedImage(controller.TitleHud.Root, "Title Background");
                Image lobbyRoom = NamedImage(controller.LobbyHud.Root, "Bedroom Background");
                Assert.That(titleRoom.sprite, Is.Not.Null);
                Assert.That(new[] { "morning", "day", "evening", "night" }, Does.Contain(titleRoom.sprite.name));
                Assert.That(titleRoom.sprite, Is.SameAs(lobbyRoom.sprite),
                    "The title and lobby keep the same randomly chosen room.");
                Assert.That(controller.ContinueGame(), Is.False);
                Assert.That(controller.StartCampaignStage(1), Is.False);
                Assert.That(controller.StartMission(), Is.False);

                controller.TitleHud.NewGameButton.onClick.Invoke();
                Assert.That(controller.TitleHud.IsConfirming, Is.False, "Without a save there is nothing to overwrite.");
                Assert.That(controller.IsPlayingCutscene, Is.True, "새 게임 opens with the awakening cutscene.");
                Assert.That(controller.IsInBriefing, Is.False);
                Assert.That(scope.Load().PrologueCleared, Is.Zero, "The new game is saved before the cutscene.");
                Assert.That(controller.SkipCutscene(), Is.True);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.IsInTitle, Is.False);
                Assert.That(controller.TitleHud.IsVisible, Is.False);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(1));
                Assert.That(controller.AutoSaveEnabled, Is.True);
                Assert.That(controller.StoryProgressionEnabled, Is.True, "새 게임 follows the story's unlocks.");
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.LaneQ), "A new story opens with the Q lane only.");
                Assert.That(controller.Campaign.StageLimit, Is.Zero, "Stages wait for the 서막.");
                GameSave saved = scope.Load();
                Assert.That(saved.PrologueCleared, Is.Zero);
                Assert.That(saved.Campaign.Currency, Is.Zero);
                Assert.That(saved.Campaign.CurriculumCompleted, Is.Empty);
                Assert.That(saved.Campaign.CurriculumActive, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator OpeningChoiceA_MasksTheFirstDestinationLineBeforeItAppears_AndSavesTheChoice()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                scope.Advance(1.1f);
                Assert.That(controller.DialogueHud.CurrentLine.Text, Does.StartWith("이룰 수 없는 꿈"));
                Assert.That(Label(controller.CutsceneHud.Root, "Cutscene Skip Hint").text, Is.EqualTo("Esc  선택으로"));

                Button close = NamedButton(controller.DialogueHud.Root, "Dialogue Close");
                close.onClick.Invoke();
                Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.EqualTo("…"));
                for (int i = 0; i < 20 && !controller.DialogueHud.IsChoosing; i++)
                {
                    if (!controller.AdvanceCutscene()) scope.Advance(2f);
                }
                Assert.That(controller.DialogueHud.IsChoosing, Is.True);
                Assert.That(NamedButton(controller.DialogueHud.Root, "Dialogue Choice A").GetComponentInChildren<Text>().text, Is.EqualTo("날 보내줘"));
                Assert.That(Label(controller.CutsceneHud.Root, "Cutscene Skip Hint").text,
                    Is.EqualTo("↑ / ↓  선택  ·  Enter  결정"));
                controller.Cutscene.Choose(0);
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Leave));
                Assert.That(controller.DialogueHud.MaskElisaName, Is.True);
                Assert.That(scope.Load().OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Leave));

                for (int i = 0; i < 20 && controller.DialogueHud.CurrentLine?.SpeakerName != "엘리사"; i++)
                {
                    if (!controller.AdvanceCutscene()) scope.Advance(2f);
                }
                Assert.That(controller.DialogueHud.CurrentLine?.SpeakerName, Is.EqualTo("엘리사"));
                Assert.That(Label(controller.DialogueHud.Root, "Dialogue Speaker").text, Is.EqualTo("???"),
                    "The identity mask must be active when the first opening line is presented.");
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("…!"));
            }
        }

        [UnityTest]
        public IEnumerator OpeningChoices_UseVerticalKeyboardNavigationWithoutLetterShortcuts()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                scope.Advance(1.1f);
                NamedButton(controller.DialogueHud.Root, "Dialogue Close").onClick.Invoke();
                for (int i = 0; i < 20 && !controller.DialogueHud.IsChoosing; i++)
                {
                    if (!controller.AdvanceCutscene()) scope.Advance(2f);
                }
                Assert.That(controller.DialogueHud.IsChoosing, Is.True);

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.aKey);
                yield return null;
                Release(keyboard.aKey);
                yield return null;
                Assert.That(controller.DialogueHud.IsChoosing, Is.True,
                    "A is an internal branch id, not a player-facing shortcut.");

                Press(keyboard.downArrowKey);
                yield return null;
                Release(keyboard.downArrowKey);
                yield return null;
                Assert.That(NamedButton(controller.DialogueHud.Root, "Dialogue Choice B").image.color,
                    Is.EqualTo(DuelVisualTheme.Accent));

                Press(keyboard.enterKey);
                yield return null;
                Release(keyboard.enterKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Essential));
                Assert.That(controller.DialogueHud.IsChoosing, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Opening_FullyRevealedLineClicks_DoNotReachTheChoiceBeforeHaHa()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                controller.Cutscene.Tick(1.1f);
                var mouse = InputSystem.AddDevice<Mouse>();
                Button next = NamedButton(controller.DialogueHud.Root, "Dialogue Next");
                yield return null;

                for (int i = 0; i < 6; i++)
                {
                    scope.Advance(5f);
                    Assert.That(controller.DialogueHud.IsRevealing, Is.False);
                    Press(mouse.leftButton);
                    yield return null;
                    scope.Advance(0f);
                    Assert.That(scope.OpeningClicks, Is.EqualTo(i + 1));
                    Release(mouse.leftButton);
                    yield return null;
                    next.onClick.Invoke();
                }

                Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.EqualTo("하하하…"));
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Full));
                Assert.That(controller.DialogueHud.IsChoosing, Is.False);
                Press(mouse.leftButton);
                yield return null;
                scope.Advance(0f);
                Assert.That(scope.OpeningClicks, Is.EqualTo(6), "The first 하하하 line closes the interruption window.");
                Release(mouse.leftButton);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Opening_TenthRapidClick_Interrupts_AndRevealClickNeverAdvancesItsLine()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                controller.Cutscene.Tick(1.1f);
                var mouse = InputSystem.AddDevice<Mouse>();
                Button next = NamedButton(controller.DialogueHud.Root, "Dialogue Next");
                yield return null;

                for (int i = 0; i < 5; i++)
                {
                    string line = controller.Cutscene.Playback.CurrentLine.Text;
                    Assert.That(controller.DialogueHud.IsRevealing, Is.True);
                    Press(mouse.leftButton);
                    yield return null;
                    scope.Advance(0f);
                    Assert.That(scope.OpeningClicks, Is.EqualTo(i * 2 + 1));
                    // Releasing after the text naturally finishes still belongs to the revealing click.
                    scope.Advance(5f);
                    Release(mouse.leftButton);
                    yield return null;
                    next.onClick.Invoke();
                    Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.EqualTo(line));

                    Press(mouse.leftButton);
                    yield return null;
                    scope.Advance(0f);
                    Assert.That(scope.OpeningClicks, Is.EqualTo(i * 2 + 2));
                    Release(mouse.leftButton);
                    yield return null;
                    next.onClick.Invoke();
                    if (i < 4)
                        Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.Not.EqualTo(line));
                }

                Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.EqualTo("…"),
                    "The tenth physical press shows the interruption and its release cannot skip the new line.");
                Assert.That(controller.DialogueHud.IsChoosing, Is.False);
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Full));
            }
        }

        [UnityTest]
        public IEnumerator OpeningReplay_FollowsTheSavedAOrBRoute_WithoutChangingTheSave()
        {
            yield return null;
            foreach (OpeningVoiceChoice selected in new[] { OpeningVoiceChoice.Leave, OpeningVoiceChoice.Essential })
            {
                using (var scope = new SaveScope())
                {
                    var prologue = new PrologueRun();
                    prologue.CompleteAll();
                    Assert.That(scope.Store.TrySave(GameSave.Capture(prologue, new CampaignRun(), selected),
                        out string error), Is.True, error);
                    string savedBeforeReplay = File.ReadAllText(scope.Store.Path);
                    DuelPrototypeController controller = scope.Controller;
                    controller.ShowTitle();
                    Assert.That(controller.ContinueGame(), Is.True);
                    Assert.That(controller.IsInLobby, Is.True);
                    Assert.That(controller.ReplayStoryCutscene(DuelPrototypeController.OpeningCutscene), Is.True);

                    var heard = new List<string>();
                    for (int turn = 0; turn < 150 && controller.IsPlayingCutscene; turn++)
                    {
                        if (controller.Cutscene.Playback.CurrentLine != null)
                        {
                            heard.Add(controller.Cutscene.Playback.CurrentLine.Text);
                            Assert.That(controller.DialogueHud.IsChoosing, Is.False,
                                "Replay must use the saved answer automatically.");
                            Assert.That(controller.AdvanceCutscene(), Is.True);
                        }
                        else scope.Advance(30f);
                    }

                    Assert.That(controller.IsInLobby, Is.True);
                    Assert.That(heard.Count, Is.GreaterThan(7));
                    Assert.That(heard[0], Does.StartWith("이룰 수 없는 꿈"));
                    Assert.That(heard[6], Is.EqualTo("…"), "Replay interrupts after the first six lines.");
                    Assert.That(heard, Does.Not.Contain("하하하…"));
                    Assert.That(heard, Does.Contain(selected == OpeningVoiceChoice.Leave
                        ? "좋아, 다만 명심해." : "이야기를 들어주겠다는 뜻으로 이해해도 될까?"));
                    Assert.That(heard.Contains("그냥."), Is.EqualTo(selected == OpeningVoiceChoice.Essential));
                    Assert.That(heard, Does.Not.Contain("그냥 모두 죽여버리면 돼."));
                    Assert.That(controller.OpeningChoice, Is.EqualTo(selected));
                    Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(savedBeforeReplay));
                }
            }
        }

        [UnityTest]
        public IEnumerator Continue_ReopensAnUnfinishedOpening_ThenCompletesTheSavedChoice()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                Assert.That(scope.Load().OpeningCompleted, Is.False,
                    "The first new-game save precedes the player's opening choice.");
                controller.ShowTitle();
                Assert.That(controller.ContinueGame(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.True);
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Full));
                scope.Advance(1.1f);
                NamedButton(controller.DialogueHud.Root, "Dialogue Close").onClick.Invoke();
                Assert.That(controller.Cutscene.Playback.CurrentLine.Text, Is.EqualTo("…"),
                    "An unfinished opening still offers the interruption after Continue.");
                for (int i = 0; i < 20 && !controller.DialogueHud.IsChoosing; i++)
                {
                    if (!controller.AdvanceCutscene()) scope.Advance(2f);
                }
                Assert.That(controller.DialogueHud.IsChoosing, Is.True);
                controller.Cutscene.Choose(1);
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Essential));
                Assert.That(scope.Load().OpeningCompleted, Is.False);
                Assert.That(scope.Load().OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Essential));

                controller.ShowTitle();
                Assert.That(controller.ContinueGame(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.True);
                var heard = new List<string>();
                for (int turn = 0; turn < 150 && controller.IsPlayingCutscene; turn++)
                {
                    if (controller.Cutscene.Playback.CurrentLine != null)
                    {
                        heard.Add(controller.Cutscene.Playback.CurrentLine.Text);
                        Assert.That(controller.AdvanceCutscene(), Is.True);
                    }
                    else scope.Advance(30f);
                }
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(heard[6], Is.EqualTo("…"));
                Assert.That(heard, Does.Not.Contain("하하하…"));
                Assert.That(heard, Does.Contain("그냥."));
                Assert.That(scope.Load().OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Essential));
                Assert.That(scope.Load().OpeningCompleted, Is.True);

                controller.ShowTitle();
                Assert.That(controller.ContinueGame(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.False,
                    "Once finished, Continue resumes at mission 1 instead of repeating the opening.");
                Assert.That(controller.IsInBriefing, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Continue_RestoresTheHiddenNameChoice()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                GameSave selected = GameSave.Capture(new PrologueRun(), new CampaignRun(), OpeningVoiceChoice.Leave);
                Assert.That(scope.Store.TrySave(selected, out string error), Is.True, error);
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.ContinueGame(), Is.True);
                Assert.That(controller.OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Leave));
                Assert.That(controller.DialogueHud.MaskElisaName, Is.True);
                Assert.That(scope.Load().OpeningChoice, Is.EqualTo(OpeningVoiceChoice.Leave));
            }
        }

        [UnityTest]
        public IEnumerator Continue_RestoresTheSave_AndLaterProgressIsSavedAgain()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                var campaign = new CampaignRun();
                Assert.That(campaign.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                for (int stage = 1; stage <= 2; stage++)
                {
                    Assert.That(campaign.TryStartStage(stage), Is.True);
                    Assert.That(campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                }
                campaign.ReturnToLobby();
                var prologue = new PrologueRun();
                prologue.CompleteAll();
                Assert.That(scope.Store.TrySave(GameSave.Capture(prologue, campaign), out string error), Is.True, error);

                controller.ShowTitle();
                Assert.That(controller.TitleHud.CanContinue, Is.True);
                Assert.That(Label(controller.TitleHud.Root, "Title Save Summary").text,
                    Does.Contain("로비").And.Contain("2 / 8").And.Contain("커리큘럼 1 / 10"));

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                Release(keyboard.enterKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.IsInLobby, Is.True, "Enter continues, and a finished arc resumes in the lobby.");
                Assert.That(controller.AutoSaveEnabled, Is.True);
                Assert.That(controller.Prologue.IsComplete, Is.True);
                Assert.That(controller.StoryProgressionEnabled, Is.True);
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.All), "Every won mission opened its feature.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(int.MaxValue), "No stage waits for a mission any more.");
                Assert.That(controller.Campaign.Currency, Is.EqualTo(campaign.Currency));
                Assert.That(controller.Campaign.IsStageCleared(2), Is.True);
                Assert.That(controller.Campaign.HighestUnlockedStage, Is.EqualTo(3));
                Assert.That(controller.Campaign.Curriculum.IsCompleted("horizontal-cut"), Is.True);
                Assert.That(controller.Campaign.OwnedSkills.Any(skill => skill.SkillId == 14), Is.True,
                    "Owned skills follow from the completed nodes.");

                Assert.That(controller.SelectCurriculumNode("diagonal-cut"), Is.True);
                GameSave afterSelect = scope.Load();
                Assert.That(afterSelect.Campaign.CurriculumActive, Is.EqualTo("diagonal-cut"), "Choosing a node is saved.");
                Assert.That(afterSelect.Campaign.CurriculumBattles, Is.Zero);
                Assert.That(afterSelect.Campaign.CurriculumCompleted, Is.EqualTo(new[] { "horizontal-cut" }));

                Assert.That(controller.StartCampaignStage(3), Is.True);
                scope.WinToSettled();
                Assert.That(controller.IsShowingResult, Is.True);
                GameSave afterBattle = scope.Load();
                Assert.That(afterBattle.Campaign.ClearedStages, Does.Contain(3), "A settled stage result is saved.");
                Assert.That(afterBattle.Campaign.Currency, Is.EqualTo(controller.Campaign.Currency));
                Assert.That(afterBattle.Campaign.CurriculumCompleted, Is.EqualTo(new[] { "horizontal-cut", "diagonal-cut" }),
                    "The node the battle completed is saved.");
                Assert.That(afterBattle.Campaign.CurriculumActive, Is.Null);
                var restored = new CampaignRun();
                Assert.That(restored.TryRestore(afterBattle.Campaign, out error), Is.True, error);
                Assert.That(restored.OwnedSkills.Any(skill => skill.SkillId == 15), Is.True, "…and so is the skill it granted.");
                Assert.That(restored.TryPlaceLoadoutSkill(15, 0, 2), Is.True, "The granted skill can be placed after loading.");

                Assert.That(controller.DismissBattleResult(), Is.True);
                controller.RestartJourney();
                GameSave afterRestart = scope.Load();
                Assert.That(afterRestart.Campaign.Currency, Is.Zero, "여정 초기화 is saved too.");
                Assert.That(afterRestart.Campaign.CurriculumCompleted, Is.Empty, "…with a reset curriculum.");
                Assert.That(afterRestart.Campaign.CurriculumActive, Is.Null);
                Assert.That(afterRestart.PrologueCleared, Is.EqualTo(StoryMissions.Count), "…and keeps the story.");
            }
        }

        [UnityTest]
        public IEnumerator MissionVictory_IsSaved_AndContinueResumesAtTheNextBriefing()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.NewGameFromTitle(), Is.True);
                Assert.That(controller.SkipCutscene(), Is.True);
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsMission, Is.True);
                scope.SetGuide(null);
                scope.WinToSettled();
                Assert.That(controller.IsPlayingScene, Is.True, "The outro plays first.");
                Assert.That(scope.Load().PrologueCleared, Is.EqualTo(1), "The win is saved before the outro.");
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsShowingResult, Is.True);

                controller.ShowTitle();
                Assert.That(Label(controller.TitleHud.Root, "Title Save Summary").text, Does.Contain("임무 2 / 4"));
                Assert.That(controller.ContinueGame(), Is.True);
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(2));
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator UnreadableSave_CannotContinue_AndNewGameConfirmsBeforeOverwriting()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Directory.CreateDirectory(Path.GetDirectoryName(scope.Store.Path));
                File.WriteAllText(scope.Store.Path, "not a save");
                controller.ShowTitle();
                Assert.That(controller.TitleHud.CanContinue, Is.False);
                Assert.That(Label(controller.TitleHud.Root, "Title Notice").text, Does.Contain("읽을 수 없어"));

                var keyboard = InputSystem.AddDevice<Keyboard>();
                controller.enabled = true;
                Press(keyboard.enterKey);
                yield return null;
                Release(keyboard.enterKey);
                yield return null;
                Assert.That(controller.TitleHud.IsConfirming, Is.True, "Enter asks before overwriting a save.");
                Assert.That(controller.IsInTitle, Is.True);
                Press(keyboard.enterKey);
                yield return null;
                Release(keyboard.enterKey);
                yield return null;
                Assert.That(controller.IsInTitle, Is.True, "Enter never confirms the overwrite.");
                Press(keyboard.escapeKey);
                yield return null;
                Release(keyboard.escapeKey);
                yield return null;
                controller.enabled = false;
                Assert.That(controller.TitleHud.IsConfirming, Is.False, "Escape cancels.");
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo("not a save"));

                controller.TitleHud.NewGameButton.onClick.Invoke();
                controller.TitleHud.CancelButton.onClick.Invoke();
                Assert.That(controller.IsInTitle, Is.True);
                controller.TitleHud.NewGameButton.onClick.Invoke();
                controller.TitleHud.ConfirmButton.onClick.Invoke();
                Assert.That(controller.SkipCutscene(), Is.True, "A confirmed 새 게임 also opens with the cutscene.");
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(scope.Load().PrologueCleared, Is.Zero, "The new game replaced the unreadable save.");
            }
        }

        [UnityTest]
        public IEnumerator DirectApiUse_NeverWritesTheSave()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                controller.StartNewGame();
                controller.RestartJourney();
                Assert.That(controller.StartCampaignStage(1), Is.True);
                scope.WinToSettled();
                Assert.That(controller.DismissBattleResult(), Is.True);
                Assert.That(controller.AutoSaveEnabled, Is.False);
                Assert.That(scope.Store.Exists, Is.False, "Only the title's choices turn saving on.");
                Assert.That(controller.StoryProgressionEnabled, Is.False);
                Assert.That(controller.Campaign.Features, Is.EqualTo(CombatFeature.All),
                    "…and the story's locks, so direct API use keeps every feature open.");
                Assert.That(controller.Campaign.StageLimit, Is.EqualTo(int.MaxValue));
            }
        }

        [UnityTest]
        public IEnumerator SaveDeletedWhileTitleShows_ContinueRefreshesTheTitleInsteadOfDoingNothing()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(scope.Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()), out string error), Is.True, error);
                controller.ShowTitle();
                Assert.That(controller.TitleHud.CanContinue, Is.True);
                Assert.That(scope.Store.Delete(), Is.True);
                Assert.That(controller.ContinueGame(), Is.False);
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.TitleHud.CanContinue, Is.False, "The title re-reads the file.");
                Assert.That(controller.AutoSaveEnabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Store_RecoversACopyLeftByAnInterruptedWrite_AndDeleteRemovesEveryCopy()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                var prologue = new PrologueRun();
                Assert.That(prologue.TryComplete(1, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(scope.Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()), out string error), Is.True, error);
                Assert.That(scope.Store.TrySave(GameSave.Capture(prologue, new CampaignRun()), out error), Is.True, error);
                Assert.That(File.Exists(scope.Store.Path + ".tmp"), Is.False);
                Assert.That(File.Exists(scope.Store.Path + ".bak"), Is.False, "A finished replace drops its backup.");
                Assert.That(scope.Load().PrologueCleared, Is.EqualTo(1), "The second save replaced the first.");

                // A replace that failed half way leaves only the new copy under the temporary name.
                File.Move(scope.Store.Path, scope.Store.Path + ".tmp");
                Assert.That(scope.Store.Exists, Is.True);
                Assert.That(scope.Load().PrologueCleared, Is.EqualTo(1));
                File.WriteAllText(scope.Store.Path + ".tmp", "broken");
                File.WriteAllText(scope.Store.Path + ".bak", GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), new CampaignRun())));
                Assert.That(scope.Load().PrologueCleared, Is.Zero, "An unreadable temporary copy falls back to the backup.");

                Assert.That(scope.Store.Delete(), Is.True);
                Assert.That(scope.Store.Exists, Is.False);
                Assert.That(File.Exists(scope.Store.Path + ".tmp") || File.Exists(scope.Store.Path + ".bak"), Is.False);
            }
        }

        private static Text Label(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate.GetComponent<Text>();
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private static Button NamedButton(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate.GetComponent<Button>();
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private static Image NamedImage(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate.GetComponent<Image>();
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly string directory;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public SaveScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath, "TitleSaveTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            public GameSave Load()
            {
                Assert.That(Store.TryLoad(out GameSave save, out string error), Is.True, error);
                Assert.That(save.Validate(out error), Is.True, error);
                return save;
            }

            public void Advance(float seconds) => advance(seconds, null);

            public int OpeningClicks => (int)typeof(DuelPrototypeController)
                .GetField("openingClickCount", PrivateInstance).GetValue(Controller);

            public void SetGuide(MissionGuide guide)
                => typeof(DuelPrototypeController).GetField("guide", PrivateInstance).SetValue(Controller, guide);

            /// <summary>Wins at once and advances until the result or a mission's outro.</summary>
            public void WinToSettled()
            {
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                typeof(DuelPrototypeController).GetField("session", PrivateInstance).SetValue(Controller,
                    new LegacyQueuedDuel(100, 50, 1, 0, LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, new object[] { null, null });
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                int frames = 0;
                while (!Controller.IsShowingResult && !Controller.IsPlayingScene && frames++ < 2000) advance(.025f, null);
                Assert.That(Controller.IsShowingResult || Controller.IsPlayingScene, Is.True);
            }

            public void Dispose()
            {
                // Turn saving off while the temporary store is still in place, then leave a fresh lobby behind.
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
