using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The academy field's areas a pickup can lie in. Presentation maps each to its exploration map.</summary>
    public enum CampaignFieldArea
    {
        /// <summary>도미니코 학원 · 복도: the stage opponents, the story board and the training dummy.</summary>
        Corridor,
        /// <summary>도미니코 학원 · 바깥 숲: the open field behind the corridor's door.</summary>
        Forest,
    }

    /// <summary>A technique lying in the academy field. Interacting with it once grants <see cref="SkillId"/>
    /// (<see cref="CampaignRun.TryCollectFieldPickup(string, out CampaignOwnedSkill)"/>). The save keeps the pickup's
    /// <see cref="Id"/>, and the owned technique follows from it, as it does from a completed curriculum node.</summary>
    public sealed class CampaignFieldPickup
    {
        internal CampaignFieldPickup(string id, int skillId, CampaignFieldArea area, float x, float y)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf(' ') >= 0) throw new ArgumentException("A pickup id is one word.", nameof(id));
            if (skillId <= 0) throw new ArgumentOutOfRangeException(nameof(skillId));
            Id = id;
            SkillId = skillId;
            Area = area;
            X = x;
            Y = y;
        }

        /// <summary>The saved id (lower-case words joined by '-'). Saves name it, so it never changes once shipped.</summary>
        public string Id { get; }
        /// <summary>The 획득 row of the skill sheet this pickup grants.</summary>
        public int SkillId { get; }
        public CampaignFieldArea Area { get; }
        /// <summary>Where it lies along the area, in the field's world units: the same ground position the walker's
        /// root uses (the art's centre is 0; the forest's walkable path runs from about -33.6 to 28.3).</summary>
        public float X { get; }
        /// <summary>Its ground height in the same units, inside the walking belt (-2.3 to -0.55).</summary>
        public float Y { get; }
    }

    /// <summary>Every technique lying in the academy field: the third way to own a 획득 row, beside the curriculum
    /// (<see cref="CampaignCurriculum"/>) and the stages' first clears. For now the four lie on the forest path, away from
    /// the school door the player arrives at (x about -27), so the field has something to find.</summary>
    public static class CampaignFieldPickups
    {
        private static readonly CampaignFieldPickup[] pickups =
        {
            new CampaignFieldPickup("forest-pierce", 47, CampaignFieldArea.Forest, -14f, -1.2f),
            new CampaignFieldPickup("forest-strike-back", 48, CampaignFieldArea.Forest, -3f, -1.85f),
            new CampaignFieldPickup("forest-sidestep", 49, CampaignFieldArea.Forest, 8f, -0.95f),
            new CampaignFieldPickup("forest-finisher", 50, CampaignFieldArea.Forest, 19f, -1.6f),
        };

        /// <summary>In field order: area, then left to right.</summary>
        public static IReadOnlyList<CampaignFieldPickup> All { get; } = Array.AsReadOnly(pickups);

        /// <summary>The pickup with this id, or null.</summary>
        public static CampaignFieldPickup Find(string id)
        {
            foreach (CampaignFieldPickup pickup in pickups)
                if (pickup.Id == id) return pickup;
            return null;
        }

        /// <summary>The pickup that grants this technique, or null.</summary>
        public static CampaignFieldPickup FindGranting(int skillId)
        {
            foreach (CampaignFieldPickup pickup in pickups)
                if (pickup.SkillId == skillId) return pickup;
            return null;
        }

        /// <summary>The pickups lying in one area, left to right.</summary>
        public static IReadOnlyList<CampaignFieldPickup> In(CampaignFieldArea area)
        {
            var found = new List<CampaignFieldPickup>();
            foreach (CampaignFieldPickup pickup in pickups)
                if (pickup.Area == area) found.Add(pickup);
            return found.AsReadOnly();
        }
    }
}
