using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPrototypeController
    {
        // The preview uses another instance of the production player, so timing changes have exactly one playback path.
        // The original session and its presentation objects stay suspended; the preview owns no save store.
        private bool combatDebugInstance, combatDebugActive;
        private int combatDebugEnemyCount = 5, combatDebugPlayerCount = 5, combatDebugLoopNumber;
        private GameObject combatDebugRoot, combatDebugSelection;
        private DuelPrototypeController combatDebugPreview;
        private DuelCombatDebugHud combatDebugHud;
        private readonly List<CombatDebugVisibility> combatDebugVisibility = new List<CombatDebugVisibility>();
        private readonly List<CombatDebugAudio> combatDebugAudio = new List<CombatDebugAudio>();

        private readonly struct CombatDebugVisibility
        {
            public readonly GameObject Root;
            public readonly bool Active;
            public CombatDebugVisibility(GameObject root) { Root = root; Active = root.activeSelf; }
        }

        private readonly struct CombatDebugAudio
        {
            public readonly AudioSource Source;
            public readonly AudioClip Clip;
            public readonly int Sample;
            public CombatDebugAudio(AudioSource source)
            {
                Source = source;
                Clip = source.clip;
                Sample = Clip != null ? source.timeSamples : 0;
            }
        }

        public bool IsCombatDebugMode => combatDebugActive;
        public DuelPrototypeController CombatDebugPreview => combatDebugPreview;
        public int CombatDebugEnemyAttackCount => combatDebugEnemyCount;
        public int CombatDebugPlayerAttackCount => combatDebugPlayerCount;
        public int CombatDebugLoopNumber => combatDebugPreview != null ? combatDebugPreview.combatDebugLoopNumber : 0;

        /// <summary>Opens a separate, repeating combat preview or restores the suspended screen. Online rooms keep their
        /// authoritative clock running, so they cannot be suspended for a local preview.</summary>
        public bool SetCombatDebugMode(bool enabled)
        {
            if (combatDebugInstance || arena == null) return false;
            if (enabled == combatDebugActive) return true;
            if (enabled && IsInOnlineVersus) return false;
            if (!enabled)
            {
                combatDebugActive = false;
                if (combatDebugRoot != null) combatDebugRoot.SetActive(false);
                RestoreCombatDebugScreen();
                return true;
            }

            combatDebugSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            EventSystem.current?.SetSelectedGameObject(null);
            combatDebugAudio.Clear();
            foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
            {
                if (!source.isPlaying) continue;
                combatDebugAudio.Add(new CombatDebugAudio(source));
                source.Pause();
            }
            combatDebugVisibility.Clear();
            for (int index = 0; index < transform.childCount; index++)
            {
                GameObject child = transform.GetChild(index).gameObject;
                // The original HUD owns the one EventSystem; the preview borrows it without replacing its action map.
                if (child == combatDebugRoot || child.GetComponent<EventSystem>() != null) continue;
                combatDebugVisibility.Add(new CombatDebugVisibility(child));
                child.SetActive(false);
            }

            try
            {
                if (combatDebugRoot == null)
                {
                    combatDebugRoot = new GameObject("Combat Debug Preview");
                    combatDebugRoot.SetActive(false);
                    combatDebugRoot.transform.SetParent(transform, false);
                    combatDebugPreview = combatDebugRoot.AddComponent<DuelPrototypeController>();
                    // Inactive creation lets Awake take the preview branch before any save or title is opened.
                    combatDebugPreview.combatDebugInstance = true;
                    combatDebugPreview.presentationSettings = presentationSettings;
                    combatDebugPreview.combatDebugEnemyCount = combatDebugEnemyCount;
                    combatDebugPreview.combatDebugPlayerCount = combatDebugPlayerCount;
                    combatDebugRoot.SetActive(true);
                    combatDebugPreview.enabled = false; // Only the owner advances the preview's clock.
                    combatDebugHud = new DuelCombatDebugHud(combatDebugRoot.transform, art,
                        SetCombatDebugEnemyAttackCount, SetCombatDebugPlayerAttackCount,
                        RestartCombatDebugPreview, () => SetCombatDebugMode(false));
                }
                else
                {
                    combatDebugRoot.SetActive(true);
                    combatDebugPreview.combatDebugLoopNumber = 0;
                    RestartCombatDebugPreview();
                }
                combatDebugActive = true;
                RefreshCombatDebugHud();
                return true;
            }
            catch
            {
                DisposeCombatDebug();
                throw;
            }
        }

        public void SetCombatDebugEnemyAttackCount(int count)
        {
            count = Mathf.Clamp(count, 0, LegacyCombatDebugSession.MaximumAttackCount);
            if (combatDebugEnemyCount == count) return;
            combatDebugEnemyCount = count;
            RestartCombatDebugPreview();
        }

        public void SetCombatDebugPlayerAttackCount(int count)
        {
            count = Mathf.Clamp(count, 0, LegacyCombatDebugSession.MaximumAttackCount);
            if (combatDebugPlayerCount == count) return;
            combatDebugPlayerCount = count;
            RestartCombatDebugPreview();
        }

        public void RestartCombatDebugPreview()
        {
            if (combatDebugPreview == null) return;
            combatDebugPreview.combatDebugEnemyCount = combatDebugEnemyCount;
            combatDebugPreview.combatDebugPlayerCount = combatDebugPlayerCount;
            combatDebugPreview.BeginCombatDebugLoop();
            RefreshCombatDebugHud();
        }

        private bool AdvanceCombatDebug(float realDelta, Keyboard keyboard)
        {
            if (combatDebugInstance) return false;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame &&
                SetCombatDebugMode(!combatDebugActive)) return true;
            if (!combatDebugActive) return false;
            combatDebugPreview.AdvancePresentation(realDelta, null);
            RefreshCombatDebugHud();
            return true;
        }

        private void BeginCombatDebugLoop()
        {
            if (!combatDebugInstance) return;
            combatDebugLoopNumber++;
            mission = null;
            guide = null;
            session = LegacyCombatDebugSession.Create(combatDebugEnemyCount, combatDebugPlayerCount);
            // Reset the same arena/HUD as a real attempt. Reset also clears reservations, so refill the preview queue.
            ResetBattlePresentation();
            for (int index = 0; index < combatDebugPlayerCount; index++) session.TryQueueLane(0);
            hud.SetStageText("전투 연출 확인");
            hud.SetEnemyName("연출 상대");
            hud.SetTurnHeaderVisible(false);
            CommitTurn();
        }

        private void RefreshCombatDebugHud()
            => combatDebugHud?.Refresh(combatDebugEnemyCount, combatDebugPlayerCount, CombatDebugLoopNumber);

        private void RestoreCombatDebugScreen()
        {
            foreach (CombatDebugVisibility state in combatDebugVisibility)
                if (state.Root != null) state.Root.SetActive(state.Active);
            combatDebugVisibility.Clear();
            foreach (CombatDebugAudio sound in combatDebugAudio)
            {
                if (sound.Source == null || !sound.Source.gameObject.activeInHierarchy) continue;
                // Child audio is stopped by SetActive(false); restore its loop position as well as its playing state.
                // Audio on the still-active controller can also own PlayOneShot voices, which UnPause preserves.
                if (sound.Source.gameObject == gameObject || sound.Clip == null) sound.Source.UnPause();
                else
                {
                    sound.Source.clip = sound.Clip;
                    sound.Source.Play();
                    if (sound.Clip.samples > 0)
                        sound.Source.timeSamples = Mathf.Clamp(sound.Sample, 0, sound.Clip.samples - 1);
                }
            }
            combatDebugAudio.Clear();
            if (combatDebugSelection != null && combatDebugSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(combatDebugSelection);
            combatDebugSelection = null;
        }

        private void DisposeCombatDebug()
        {
            combatDebugActive = false;
            if (combatDebugRoot != null) combatDebugRoot.SetActive(false);
            RestoreCombatDebugScreen();
            combatDebugHud?.Dispose();
            combatDebugHud = null;
            if (combatDebugRoot != null) Object.Destroy(combatDebugRoot);
            combatDebugRoot = null;
            combatDebugPreview = null;
        }
    }
}
