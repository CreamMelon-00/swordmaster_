using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class OnlineVersusEntryPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator OnlineTitleButton_OpensInviteRoom_LeaveKeepsCampaignSave()
        {
            yield return null;
            using (var scope = new SaveScope())
            {
                string before = scope.CreateSaveAndReadText();
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Application.runInBackground = false;

                Assert.That(controller.TitleHud.OnlineVersusButton.interactable, Is.True);
                controller.TitleHud.OnlineVersusButton.onClick.Invoke();
                Assert.That(controller.IsInOnlineVersus, Is.True);
                Assert.That(controller.IsInLocalVersus, Is.False);
                Assert.That(controller.IsInTitle, Is.False);
                Assert.That(controller.OnlineVersus.Hud.IsVisible, Is.True);
                Assert.That(controller.OnlineVersus.IsInBattle, Is.False);
                Assert.That(controller.AutoSaveEnabled, Is.False);
                Assert.That(Application.runInBackground, Is.True,
                    "The online room must stay connected while the window is unfocused.");
                Assert.That(controller.StartOnlineVersus(), Is.False,
                    "The title entry must not start a second invite room while one is open.");
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(before));

                controller.OnlineVersus.Hud.LeaveButton.onClick.Invoke();
                Assert.That(controller.IsInOnlineVersus, Is.False);
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.TitleHud.IsVisible, Is.True);
                Assert.That(controller.OnlineVersus.Hud.IsVisible, Is.False);
                Assert.That(Application.runInBackground, Is.False,
                    "Leaving online play must restore the previous application setting.");
                Assert.That(File.ReadAllText(scope.Store.Path), Is.EqualTo(before));
            }
        }

        [UnityTest]
        public IEnumerator EscapeFromInviteRoom_ReturnsToTitleWithoutCreatingSave()
        {
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Application.runInBackground = false;
                Assert.That(scope.Store.Exists, Is.False);
                Assert.That(controller.StartOnlineVersus(), Is.True);
                Assert.That(Application.runInBackground, Is.True);
                Assert.That(controller.OnlineVersus.Hud.IsVisible, Is.True);

                controller.enabled = true;
                Press(keyboard.escapeKey);
                yield return null;
                Release(keyboard.escapeKey);
                yield return null;
                controller.enabled = false;

                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.IsInOnlineVersus, Is.False);
                Assert.That(controller.TitleHud.IsVisible, Is.True);
                Assert.That(controller.OnlineVersus.Hud.IsVisible, Is.False);
                Assert.That(Application.runInBackground, Is.False);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator MissingCloudProject_ShowsActionableErrorAndCanLeave()
        {
            yield return null;
            if (!string.IsNullOrEmpty(Application.cloudProjectId))
                Assert.Ignore("This offline test only runs before a Unity Cloud project is linked.");

            using (var scope = new SaveScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ShowTitle();
                Assert.That(controller.StartOnlineVersus(), Is.True);
                controller.OnlineVersus.Hud.CreateButton.onClick.Invoke();
                yield return null;

                string message = controller.OnlineVersus.Hud.Root.transform
                    .Find("Invite Border/Invite Card/Invite Message")
                    .GetComponent<UnityEngine.UI.Text>().text;
                Assert.That(message, Does.Contain("Unity Cloud 프로젝트"));
                Assert.That(controller.OnlineVersus.Hud.IsVisible, Is.True);
                Assert.That(controller.OnlineVersus.IsInBattle, Is.False);
                Assert.That(controller.OnlineVersus.Hud.CreateButton.interactable, Is.True,
                    "An error should let the player retry after linking a project.");

                controller.OnlineVersus.Hud.LeaveButton.onClick.Invoke();
                Assert.That(controller.IsInTitle, Is.True);
                Assert.That(controller.IsInOnlineVersus, Is.False);
                Assert.That(scope.Store.Exists, Is.False);
            }
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly GameSaveStore originalStore;
            private readonly bool originalEnabled;
            private readonly bool originalRunInBackground;
            private readonly string directory;

            public DuelPrototypeController Controller { get; }
            public GameSaveStore Store { get; }

            public SaveScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalRunInBackground = Application.runInBackground;
                Controller.enabled = false;
                originalStore = Controller.SaveStore;
                directory = Path.Combine(Application.temporaryCachePath,
                    "OnlineVersusEntryTests-" + Guid.NewGuid().ToString("N"));
                Store = new GameSaveStore(Path.Combine(directory, GameSaveStore.FileName));
                Controller.SaveStore = Store;
            }

            public string CreateSaveAndReadText()
            {
                Assert.That(Store.TrySave(GameSave.Capture(new PrologueRun(), new CampaignRun()),
                    out string error), Is.True, error);
                return File.ReadAllText(Store.Path);
            }

            public void Dispose()
            {
                Controller.enabled = false;
                Controller.ShowTitle();
                Controller.SaveStore = originalStore;
                Controller.enabled = originalEnabled;
                Application.runInBackground = originalRunInBackground;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
