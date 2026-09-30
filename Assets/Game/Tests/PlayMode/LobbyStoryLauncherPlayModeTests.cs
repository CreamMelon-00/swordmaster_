using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Dialogue;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LobbyStoryLauncherPlayModeTests
    {
        [UnityTest]
        public IEnumerator Launcher_IsSingleAndVisibleOnlyOnLobbyHome()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);
            Assert.That(Object.FindObjectsByType<LobbyStoryLauncher>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length, Is.EqualTo(1));

            try
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
                yield return null;

                Assert.That(launcher.IsVisible, Is.True);
                Assert.That(launcher.StoryButton, Is.Not.Null);
                Assert.That(launcher.StoryButton.interactable, Is.EqualTo(launcher.IsStoryReady));
                Assert.That(launcher.StoryButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(launcher.Root.transform.parent, Is.SameAs(controller.LobbyHud.Root.transform));

                LobbyTab[] hiddenTabs = { LobbyTab.Stages, LobbyTab.Loadout, LobbyTab.Curriculum };
                for (int index = 0; index < hiddenTabs.Length; index++)
                {
                    controller.LobbyHud.ShowTab(hiddenTabs[index]);
                    yield return null;
                    Assert.That(launcher.IsVisible, Is.False,
                        $"Story entry must be hidden on {hiddenTabs[index]}.");
                }

                controller.LobbyHud.ShowTab(LobbyTab.Home);
                yield return null;
                Assert.That(launcher.IsVisible, Is.True);
            }
            finally
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
            }
        }

        [UnityTest]
        public IEnumerator StoryButton_StartsActualDialogueAndRestoresHomeAfterCompletion()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            LobbyStoryLauncher launcher = Object.FindAnyObjectByType<LobbyStoryLauncher>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(launcher, Is.Not.Null);

            int stage = controller.Campaign.StageNumber;
            int currency = controller.Campaign.Currency;
            int curriculumDone = controller.Campaign.Curriculum.CompletedCount;
            string curriculumActive = controller.Campaign.Curriculum.Active?.Id;
            try
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
                launcher.RefreshAvailability();
                yield return null;
                if (!launcher.IsStoryReady)
                    Assert.Ignore("Production story is not ready: " + launcher.AvailabilityMessage);

                launcher.StoryButton.onClick.Invoke();
                yield return null;

                Assert.That(controller.IsShowingDialogue, Is.True);
                Assert.That(controller.CurrentDialogueLine, Is.Not.Null);
                Assert.That(controller.DialogueHud.IsVisible, Is.True);
                Assert.That(launcher.IsVisible, Is.False);

                controller.CloseDialogue();
                yield return null;

                Assert.That(controller.IsShowingDialogue, Is.False);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.LobbyHud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(launcher.IsVisible, Is.True);
                Assert.That(launcher.StoryButton.interactable, Is.True);
                Assert.That(controller.Campaign.StageNumber, Is.EqualTo(stage));
                Assert.That(controller.Campaign.Currency, Is.EqualTo(currency));
                Assert.That(controller.Campaign.Curriculum.CompletedCount, Is.EqualTo(curriculumDone));
                Assert.That(controller.Campaign.Curriculum.Active?.Id, Is.EqualTo(curriculumActive));
            }
            finally
            {
                controller.CloseDialogue();
                controller.ReturnToLobby();
                controller.LobbyHud.ShowTab(LobbyTab.Home);
            }
        }

        [UnityTest]
        public IEnumerator ProductionStoryResource_IsPresentAndValid()
        {
            yield return null;
            TextAsset source = Resources.Load<TextAsset>(LobbyStoryLauncher.DialogueResourcePath);
            Assert.That(source, Is.Not.Null,
                $"Missing Resources/{LobbyStoryLauncher.DialogueResourcePath}.txt");
            Assert.That(string.IsNullOrWhiteSpace(source.text), Is.False,
                "The production story entry document is empty.");

            DialogueScript script = null;
            Assert.DoesNotThrow(() => script = DialogueScriptParser.Parse(
                LobbyStoryLauncher.DialogueResourcePath, source.text));
            Assert.That(script, Is.Not.Null);
            Assert.That(script.Lines, Is.Not.Empty);
        }
    }
}
