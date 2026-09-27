using UnityEngine;

namespace TurnLimbo.Presentation
{
    public static class DuelPrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototypeIfMissing()
        {
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            if (controller == null)
            {
                var host = new GameObject("Duel Prototype");
                controller = host.AddComponent<DuelPrototypeController>();
            }

            if (controller.GetComponent<LobbyStoryLauncher>() == null)
                controller.gameObject.AddComponent<LobbyStoryLauncher>();
        }
    }
}
