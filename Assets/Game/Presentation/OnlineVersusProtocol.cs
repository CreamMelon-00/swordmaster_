using System;
using System.Text;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public enum OnlineVersusAction { QueueLane, Cycle, Breath, Pass }
    public enum OnlineVersusMessageKind { Hello, Ready, Start, Request, Applied, Clock, TurnDigest, Rematch, Error }

    /// <summary>A small, versioned command envelope. The transport and session validate sender identity separately.</summary>
    [Serializable]
    public sealed class OnlineVersusMessage
    {
        public const int CurrentProtocolVersion = 1;
        public const int MaximumPayloadBytes = 4096;

        public int ProtocolVersion = CurrentProtocolVersion;
        public OnlineVersusMessageKind Kind;
        public string MatchId = string.Empty;
        public int Sequence;
        public int Round;
        public int Player;
        public OnlineVersusAction Action;
        public int Lane;
        public int Seed;
        public int OpeningPlayer;
        public float LeftClock;
        public float RightClock;
        public bool LeftReady;
        public bool RightReady;
        public string RulesHash = string.Empty;
        public string StateHash = string.Empty;
        public string Text = string.Empty;

        public string Encode()
        {
            if (!Validate(out string error))
                throw new InvalidOperationException("Invalid online versus message: " + error);
            string json = JsonUtility.ToJson(this);
            if (Encoding.UTF8.GetByteCount(json) > MaximumPayloadBytes)
                throw new InvalidOperationException("Online versus message exceeds 4 KB.");
            return json;
        }

        public static bool TryDecode(string json, out OnlineVersusMessage message)
            => TryDecode(json, out message, out _);

        public static bool TryDecode(string json, out OnlineVersusMessage message, out string error)
        {
            message = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty message.";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(json) > MaximumPayloadBytes)
            {
                error = "Message exceeds 4 KB.";
                return false;
            }
            // JsonUtility accepts omitted fields as their default values. These fields are essential to
            // distinguishing a real Hello from an empty or accidentally truncated JSON object.
            if (json.IndexOf("\"ProtocolVersion\"", StringComparison.Ordinal) < 0 ||
                json.IndexOf("\"Kind\"", StringComparison.Ordinal) < 0)
            {
                error = "Message is missing its version or kind.";
                return false;
            }
            try
            {
                message = JsonUtility.FromJson<OnlineVersusMessage>(json);
            }
            catch (Exception exception)
            {
                error = "Invalid JSON: " + exception.GetType().Name;
                return false;
            }
            if (message == null || !message.Validate(out error))
            {
                message = null;
                if (error == null) error = "Invalid message.";
                return false;
            }
            return true;
        }

        private bool Validate(out string error)
        {
            error = null;
            if (ProtocolVersion != CurrentProtocolVersion)
                return Invalid("Unsupported protocol version.", out error);
            if (!Enum.IsDefined(typeof(OnlineVersusMessageKind), Kind))
                return Invalid("Unknown message kind.", out error);
            if (!Enum.IsDefined(typeof(OnlineVersusAction), Action))
                return Invalid("Unknown action.", out error);
            if (Sequence < 0 || Sequence > 1000000 || Round < 0 || Round > 20)
                return Invalid("Sequence or round is out of range.", out error);
            if (Player < -1 || Player > 1 || OpeningPlayer < 0 || OpeningPlayer > 1)
                return Invalid("Player is out of range.", out error);
            if (Lane < -1 || Lane > 2)
                return Invalid("Lane is out of range.", out error);
            if (Action == OnlineVersusAction.QueueLane &&
                (Kind == OnlineVersusMessageKind.Request || Kind == OnlineVersusMessageKind.Applied) &&
                Lane < 0)
                return Invalid("Queued skill requires a lane.", out error);
            if (!IsValidClock(LeftClock) || !IsValidClock(RightClock))
                return Invalid("Clock is out of range.", out error);
            if (!ValidText(MatchId, 128) || !ValidText(Text, 256))
                return Invalid("Identifier or text is too long or contains control characters.", out error);
            if (!ValidHash(RulesHash) || !ValidHash(StateHash))
                return Invalid("Invalid hash.", out error);
            if (RequiresMatchId(Kind) && string.IsNullOrEmpty(MatchId))
                return Invalid("Match identifier is required.", out error);
            if ((Kind == OnlineVersusMessageKind.Request || Kind == OnlineVersusMessageKind.Applied) &&
                (Round == 0 || Player < 0))
                return Invalid("Action requires a round and player.", out error);
            return true;
        }

        private static bool RequiresMatchId(OnlineVersusMessageKind kind)
            => kind == OnlineVersusMessageKind.Start || kind == OnlineVersusMessageKind.Request ||
                kind == OnlineVersusMessageKind.Applied || kind == OnlineVersusMessageKind.Clock ||
                kind == OnlineVersusMessageKind.TurnDigest || kind == OnlineVersusMessageKind.Rematch;

        private static bool IsValidClock(float seconds)
            => !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f && seconds <= 60f;

        private static bool ValidText(string value, int maximumLength)
        {
            if (value == null || value.Length > maximumLength) return false;
            for (int i = 0; i < value.Length; i++)
                if (char.IsControl(value[i])) return false;
            return true;
        }

        private static bool ValidHash(string hash)
        {
            if (hash == null) return false;
            if (hash.Length == 0) return true;
            if (hash.Length != 16) return false;
            for (int i = 0; i < hash.Length; i++)
                if (!Uri.IsHexDigit(hash[i])) return false;
            return true;
        }

        private static bool Invalid(string reason, out string error)
        {
            error = reason;
            return false;
        }
    }

    /// <summary>Stable hashes for the skill data and observable duel state; no private account data is included.</summary>
    public static class OnlineVersusProtocol
    {
        // Increment when the combat simulation changes without a corresponding skill-sheet edit.
        public const string CombatRulesVersion = "local-versus-rules-1";
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static string ComputeRulesHash()
        {
            TextAsset sheet = Resources.Load<TextAsset>(LegacySkillDefinitions.SheetResourcePath);
            if (sheet == null) throw new InvalidOperationException("The online versus skill sheet is missing.");
            return ComputeRulesHashForContent(sheet.bytes, Application.version);
        }

        /// <summary>
        /// Hashes the skill sheet's UTF-8 bytes after normalizing line endings. Git may check out the
        /// same CSV with LF, CRLF, or mixed endings on different machines.
        /// </summary>
        public static string ComputeRulesHashForContent(byte[] content, string applicationVersion)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (applicationVersion == null) throw new ArgumentNullException(nameof(applicationVersion));

            byte[] normalized = new byte[content.Length];
            int length = 0;
            for (int i = 0; i < content.Length; i++)
            {
                byte value = content[i];
                if (value == '\r')
                {
                    normalized[length++] = (byte)'\n';
                    if (i + 1 < content.Length && content[i + 1] == '\n') i++;
                }
                else normalized[length++] = value;
            }

            ulong hash = Offset;
            AddString(ref hash, CombatRulesVersion);
            AddString(ref hash, applicationVersion);
            AddInt(ref hash, length);
            for (int i = 0; i < length; i++) AddByte(ref hash, normalized[i]);
            return hash.ToString("x16");
        }

        /// <summary>
        /// Hashes the public battle state after an action or turn. Both peers must use the same rules and seed;
        /// the host remains authoritative because this digest does not expose hidden RNG or private buff fields.
        /// </summary>
        public static string ComputeStateHash(LocalVersusMatch match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            ulong hash = Offset;
            AddInt(ref hash, OnlineVersusMessage.CurrentProtocolVersion);
            AddInt(ref hash, match.RoundNumber);
            AddInt(ref hash, (int)match.Phase);
            AddInt(ref hash, (int)match.Outcome);
            AddInt(ref hash, match.OpeningPlayer);
            AddInt(ref hash, match.CurrentPlanner);
            AddInt(ref hash, match.ConsecutivePasses);
            AddInt(ref hash, match.LastPassPlayer);
            AddInt(ref hash, match.ResolutionSlotCount);
            AddSlot(ref hash, match.CurrentSlot);
            AddFighter(ref hash, match.Left);
            AddFighter(ref hash, match.Right);
            return hash.ToString("x16");
        }

        private static void AddFighter(ref ulong hash, LocalVersusFighter fighter)
        {
            AddInt(ref hash, fighter.State.Health);
            AddInt(ref hash, fighter.State.Resistance);
            AddInt(ref hash, fighter.Act);
            AddInt(ref hash, fighter.NextActGain);
            AddInt(ref hash, fighter.BreathsQueuedThisTurn);
            AddInt(ref hash, fighter.LaneCyclesThisTurn);
            for (int lane = 0; lane < 3; lane++)
            {
                var skills = fighter.GetLane(lane);
                AddInt(ref hash, skills.Count);
                for (int i = 0; i < skills.Count; i++) AddInt(ref hash, skills[i].Id);
            }
            AddInt(ref hash, fighter.Queue.Count);
            for (int i = 0; i < fighter.Queue.Count; i++) AddInt(ref hash, fighter.Queue[i].Id);

            // The initial nine skills are the only roster in this equal-start online mode.
            var initial = LegacyInitialSkills.All;
            AddInt(ref hash, initial.Count);
            for (int i = 0; i < initial.Count; i++)
            {
                AddInt(ref hash, initial[i].Id);
                AddInt(ref hash, fighter.CycleUses(initial[i].Id));
            }
        }

        private static void AddSlot(ref ulong hash, LocalVersusSlot slot)
        {
            AddInt(ref hash, slot == null ? -1 : slot.SlotIndex);
            if (slot == null) return;
            AddInt(ref hash, slot.HitCount);
            AddInt(ref hash, slot.HitsResolved);
            AddInt(ref hash, slot.LeftSkill == null ? 0 : slot.LeftSkill.Id);
            AddInt(ref hash, slot.RightSkill == null ? 0 : slot.RightSkill.Id);
        }

        private static void AddString(ref ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            AddInt(ref hash, bytes.Length);
            for (int i = 0; i < bytes.Length; i++) AddByte(ref hash, bytes[i]);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
                AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= Prime;
            }
        }
    }
}

