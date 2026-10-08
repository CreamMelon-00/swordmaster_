using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>맞물림 on the duel HUD (<see cref="LegacyMeshing"/> has the rule). Each meshed card of the player's queue row
    /// carries a small brass tab under its bottom edge, a gear and its chain's bonus ("+60%"), read from the duel every
    /// frame: live while planning, so the marks change as skills are queued, and the committed queue while it resolves. A
    /// mark that appears or rises pops. The held skill's explanation gets one row over its hint while the duel can mesh
    /// (two lanes or more, a percent above 0): what queueing that skill now would earn, or how to earn it. The enemy's row
    /// and its explanation show nothing of it. The bursts and the first hit's flash are <see cref="DuelMeshCues"/>'.</summary>
    public sealed partial class LegacyCombatHud
    {
        /// <summary>The held explanation's 맞물림 row when queueing its skill now would not mesh.</summary>
        public const string MeshNoteHint = "맞물림 · 다른 열 기술 바로 뒤에 예약하면 위력 상승";
        // The note's row over the hint (HUD units before the explanation's scale), and the mark gear's texture size.
        private const float MeshNoteRow = 22f;
        private const int MeshGearTexels = 64;
        private readonly List<int> meshBonuses = new List<int>();
        private Texture2D meshGearTexture;
        private Sprite meshGearSprite;
        private RectTransform playerMeshNote;
        private Text playerMeshNoteText;
        private int shownMeshNoteBonus = -1;

        /// <summary>The player's queue row: the cards' parent over the player's status panel. 맞물림's bursts play beside it
        /// (<see cref="DuelMeshCues"/>), never in it, so its children stay its cards.</summary>
        public RectTransform PlayerQueueRow => disposed ? null : playerQueue.Root;

        /// <summary>맞물림's gear: the marks' and <see cref="DuelMeshCues"/>' (<see cref="LegacyMeshCue.GearTeeth"/> teeth,
        /// drawn in white for an Image to tint).</summary>
        public Sprite MeshGearSprite => meshGearSprite;

        /// <summary>The 맞물림 mark under the player's queued card <paramref name="queueIndex"/>, or null while it shows none.</summary>
        public RectTransform GetMeshMark(int queueIndex) => disposed ? null : playerQueue.GetMeshMark(queueIndex);

        /// <summary>The bonus the player's queued card <paramref name="queueIndex"/> shows ("+40%"), or null while it shows none.</summary>
        public string GetMeshMarkLabel(int queueIndex) => disposed ? null : playerQueue.GetMeshLabel(queueIndex);

        /// <summary>The held explanation's 맞물림 row (null text while it is not shown).</summary>
        public string MeshNote => disposed || !playerMeshNote.gameObject.activeSelf ? null : playerMeshNoteText.text;

        /// <summary>The held explanation's 맞물림 row: what queueing the skill now earns, or how to earn it.</summary>
        public static string MeshNoteText(int bonusPercent)
            => bonusPercent > 0 ? "맞물림 · 지금 예약하면 위력 " + LegacyMeshCue.BonusLabel(bonusPercent) : MeshNoteHint;

        private void BuildMeshPictures()
        {
            meshGearTexture = GearTexture("Mesh Gear", MeshGearTexels, LegacyMeshCue.GearTeeth, LegacyGearShimmer.RootRadius,
                LegacyGearShimmer.RimInnerRadius, LegacyGearShimmer.HubRadius, LegacyGearShimmer.AxleRadius, 4);
            meshGearSprite = DuelGearShimmer.CreateSprite(meshGearTexture);
        }

        private void ReleaseMeshPictures()
        {
            DuelGearShimmer.Release(meshGearSprite);
            DuelGearShimmer.Release(meshGearTexture);
            meshGearSprite = null;
            meshGearTexture = null;
        }

        // The row over the held explanation's hint: a small gear and a line, hidden until a duel that can mesh shows it.
        private void BuildMeshNote(float scale)
        {
            playerMeshNote = Rect("Mesh Note", playerExplanation, Vector2.zero, new Vector2(416f, MeshNoteRow) * scale, Vector2.one * .5f);
            Image("Mesh Note Gear", playerMeshNote, meshGearSprite, new Vector2(-199f, 0f) * scale, Vector2.one * 16f * scale,
                DuelVisualTheme.Ink);
            playerMeshNoteText = Text("Mesh Note Text", playerMeshNote, new Vector2(12f, 0f) * scale, new Vector2(392f, MeshNoteRow) * scale,
                ExplanationType(14), TextAnchor.MiddleLeft);
            playerMeshNoteText.color = DuelVisualTheme.Ink;
            playerMeshNoteText.supportRichText = false;
            FitExplanationLabel(playerMeshNoteText, ExplanationType(11));
            playerMeshNote.gameObject.SetActive(false);
        }

        /// <summary>Brings the held explanation's 맞물림 row up to date for <paramref name="skill"/>, the front of its lane:
        /// shown while the duel can mesh, telling the bonus queueing it now would earn (it follows the queue, ACT and
        /// 넘기기). True when the row came or went, so the card is laid out again.</summary>
        private bool UpdateMeshNote(LegacySkill skill)
        {
            bool shown = displayedSession != null && displayedSession.MeshPercent > 0 && displayedSession.Features.LaneCount() >= 2;
            bool toggled = playerMeshNote.gameObject.activeSelf != shown;
            if (toggled) playerMeshNote.gameObject.SetActive(shown);
            if (!shown) return toggled;
            int bonus = displayedSession.PlayerMeshIfQueued(skill.LaneIndex).BonusPercent;
            if (bonus != shownMeshNoteBonus)
            {
                shownMeshNoteBonus = bonus;
                playerMeshNoteText.text = MeshNoteText(bonus);
                playerMeshNoteText.fontStyle = bonus > 0 ? FontStyle.Bold : FontStyle.Normal;
            }
            return toggled;
        }

        private void ResetMeshNote()
        {
            shownMeshNoteBonus = -1;
            playerMeshNote.gameObject.SetActive(false);
        }

        // Each of the player's slots' bonus, for the cards' marks: the live queue while planning, the committed one while
        // it resolves (LegacyQueuedDuel.PlayerMesh). 0 for a slot not in a chain.
        private void SetMeshMarks(LegacyQueuedDuel session)
        {
            meshBonuses.Clear();
            for (int slot = 0; slot < session.PlayerQueue.Count; slot++) meshBonuses.Add(session.PlayerMesh(slot).BonusPercent);
            playerQueue.SetMeshBonuses(meshBonuses);
        }
    }
}
