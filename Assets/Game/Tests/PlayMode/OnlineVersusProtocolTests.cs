using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class OnlineVersusProtocolTests
    {
        [Test]
        public void Request_RoundTripsWithVersionAndCommand()
        {
            var outgoing = new OnlineVersusMessage
            {
                Kind = OnlineVersusMessageKind.Request,
                MatchId = "match_42",
                Sequence = 7,
                Round = 2,
                Player = 1,
                Action = OnlineVersusAction.QueueLane,
                Lane = 2,
                LeftClock = 18.5f,
                RightClock = 12.25f,
                RulesHash = OnlineVersusProtocol.ComputeRulesHash()
            };

            string json = outgoing.Encode();
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(json),
                Is.LessThanOrEqualTo(OnlineVersusMessage.MaximumPayloadBytes));
            Assert.That(OnlineVersusMessage.TryDecode(json, out OnlineVersusMessage received, out string error),
                Is.True, error);
            Assert.That(received.ProtocolVersion, Is.EqualTo(OnlineVersusMessage.CurrentProtocolVersion));
            Assert.That(received.Kind, Is.EqualTo(OnlineVersusMessageKind.Request));
            Assert.That(received.MatchId, Is.EqualTo("match_42"));
            Assert.That(received.Sequence, Is.EqualTo(7));
            Assert.That(received.Round, Is.EqualTo(2));
            Assert.That(received.Player, Is.EqualTo(1));
            Assert.That(received.Action, Is.EqualTo(OnlineVersusAction.QueueLane));
            Assert.That(received.Lane, Is.EqualTo(2));
            Assert.That(received.RulesHash, Is.EqualTo(outgoing.RulesHash));
        }

        [Test]
        public void ReadyAndStart_RoundTripSeparateOrderedLoadouts()
        {
            var left = new VersusLoadout();
            var right = new VersusLoadout();
            Assert.That(right.TryPlaceSkill(right.GetSkillId(0, 1), 0, 0), Is.True);
            Assert.That(right.ExportIds(), Is.Not.EqualTo(left.ExportIds()));

            var ready = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Ready, Player = 1, Sequence = 1,
                RightReady = true, RightSkillIds = right.ExportIds()
            };
            Assert.That(OnlineVersusMessage.TryDecode(ready.Encode(), out OnlineVersusMessage readyCopy), Is.True);
            Assert.That(readyCopy.RightSkillIds, Is.EqualTo(right.ExportIds()));

            var start = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Start, MatchId = "match_loadout",
                Seed = 43, OpeningPlayer = 1, RulesHash = OnlineVersusProtocol.ComputeRulesHash(),
                LeftSkillIds = left.ExportIds(), RightSkillIds = right.ExportIds()
            };
            Assert.That(OnlineVersusMessage.TryDecode(start.Encode(), out OnlineVersusMessage startCopy), Is.True);
            Assert.That(startCopy.LeftSkillIds, Is.EqualTo(left.ExportIds()));
            Assert.That(startCopy.RightSkillIds, Is.EqualTo(right.ExportIds()));
            Assert.That(startCopy.LeftSkillIds, Is.Not.EqualTo(startCopy.RightSkillIds));
        }

        [Test]
        public void ReadinessCancellation_RoundTripsWithoutLoadout_AndAcknowledgesItsRevision()
        {
            var cancel = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Ready, Player = 1, Sequence = 2, RightReady = false
            };
            Assert.That(OnlineVersusMessage.TryDecode(cancel.Encode(), out OnlineVersusMessage request), Is.True);
            Assert.That(request.ProtocolVersion, Is.EqualTo(3));
            Assert.That(request.Sequence, Is.EqualTo(2));
            Assert.That(request.RightReady, Is.False);
            Assert.That(request.RightSkillIds, Is.Empty);

            var acknowledgement = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Ready, Sequence = request.Sequence,
                LeftReady = true, RightReady = false
            };
            Assert.That(OnlineVersusMessage.TryDecode(acknowledgement.Encode(), out OnlineVersusMessage response), Is.True);
            Assert.That(response.Sequence, Is.EqualTo(request.Sequence));
            Assert.That(response.LeftReady, Is.True);
            Assert.That(response.RightReady, Is.False);
        }

        [Test]
        public void ReadinessRevision_IsRequiredForGuestRequests_AndOldPeersAreRejected()
        {
            var unsequenced = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Ready, Player = 1, RightReady = false
            };
            AssertRejected(JsonUtility.ToJson(unsequenced));
            var oldPeer = new OnlineVersusMessage {
                ProtocolVersion = 2, Kind = OnlineVersusMessageKind.Hello
            };
            AssertRejected(JsonUtility.ToJson(oldPeer));
        }

        [Test]
        public void Decoder_RejectsMalformedOversizedUnsupportedAndOutOfRangeMessages()
        {
            AssertRejected(string.Empty);
            AssertRejected("{}");
            AssertRejected("{bad json}");
            AssertRejected(new string('x', OnlineVersusMessage.MaximumPayloadBytes + 1));

            var invalid = new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Hello };
            invalid.ProtocolVersion = OnlineVersusMessage.CurrentProtocolVersion + 1;
            AssertRejected(JsonUtility.ToJson(invalid));
            invalid.ProtocolVersion = OnlineVersusMessage.CurrentProtocolVersion;
            invalid.Kind = (OnlineVersusMessageKind)999;
            AssertRejected(JsonUtility.ToJson(invalid));
            invalid.Kind = OnlineVersusMessageKind.Hello;
            invalid.Player = 2;
            AssertRejected(JsonUtility.ToJson(invalid));
            invalid.Player = 0;
            invalid.RulesHash = "not-a-hash";
            AssertRejected(JsonUtility.ToJson(invalid));

            invalid.RulesHash = string.Empty;
            invalid.Kind = OnlineVersusMessageKind.Request;
            invalid.MatchId = "match";
            invalid.Round = 1;
            invalid.Action = OnlineVersusAction.QueueLane;
            invalid.Lane = -1;
            AssertRejected(JsonUtility.ToJson(invalid));
            invalid.Lane = 0;
            invalid.LeftClock = 61f;
            AssertRejected(JsonUtility.ToJson(invalid));
            invalid.LeftClock = 0f;
            invalid.Round = 21;
            AssertRejected(JsonUtility.ToJson(invalid));

            var missingGuestLoadout = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Ready, Player = 1, Sequence = 1, RightReady = true
            };
            AssertRejected(JsonUtility.ToJson(missingGuestLoadout));
            var missingStartLoadouts = new OnlineVersusMessage {
                Kind = OnlineVersusMessageKind.Start, MatchId = "match"
            };
            AssertRejected(JsonUtility.ToJson(missingStartLoadouts));
            missingStartLoadouts.LeftSkillIds = new[] { 1, 2 };
            missingStartLoadouts.RightSkillIds = new VersusLoadout().ExportIds();
            AssertRejected(JsonUtility.ToJson(missingStartLoadouts));
        }

        [Test]
        public void RulesHash_UsesBundledSkillSheetAndBuildRulesVersion()
        {
            TextAsset sheet = Resources.Load<TextAsset>(LegacySkillDefinitions.SheetResourcePath);
            Assert.That(sheet, Is.Not.Null);
            string first = OnlineVersusProtocol.ComputeRulesHash();
            Assert.That(first, Does.Match("^[0-9a-f]{16}$"));
            Assert.That(OnlineVersusProtocol.ComputeRulesHash(), Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(ReferenceRulesHash(sheet.bytes, Application.version,
                OnlineVersusProtocol.CombatRulesVersion)));
            Assert.That(first, Is.Not.EqualTo(ReferenceRulesHash(sheet.bytes, Application.version + ".other",
                OnlineVersusProtocol.CombatRulesVersion)));
            Assert.That(first, Is.Not.EqualTo(ReferenceRulesHash(sheet.bytes, Application.version,
                OnlineVersusProtocol.CombatRulesVersion + "-next")));
        }

        [Test]
        public void RulesHash_TreatsLfCrLfAndMixedLineEndingsAsSameContent()
        {
            const string lf = "id,name\n1,가\n2,나\n";
            const string crlf = "id,name\r\n1,가\r\n2,나\r\n";
            const string mixed = "id,name\r\n1,가\n2,나\r";
            byte[] lfBytes = System.Text.Encoding.UTF8.GetBytes(lf);
            string expected = OnlineVersusProtocol.ComputeRulesHashForContent(lfBytes, "1.0");

            Assert.That(OnlineVersusProtocol.ComputeRulesHashForContent(
                System.Text.Encoding.UTF8.GetBytes(crlf), "1.0"), Is.EqualTo(expected));
            Assert.That(OnlineVersusProtocol.ComputeRulesHashForContent(
                System.Text.Encoding.UTF8.GetBytes(mixed), "1.0"), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo(ReferenceRulesHash(lfBytes, "1.0",
                OnlineVersusProtocol.CombatRulesVersion)));
            Assert.That(OnlineVersusProtocol.ComputeRulesHashForContent(
                System.Text.Encoding.UTF8.GetBytes("id,name\n1,가\n2,다\n"), "1.0"),
                Is.Not.EqualTo(expected));
            Assert.That(OnlineVersusProtocol.ComputeRulesHashForContent(lfBytes, "1.1"),
                Is.Not.EqualTo(expected));
        }

        [Test]
        public void StateHash_IsDeterministicAndChangesAfterPublicActions()
        {
            var left = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 37);
            var right = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 37);
            string initial = OnlineVersusProtocol.ComputeStateHash(left);
            Assert.That(initial, Does.Match("^[0-9a-f]{16}$"));
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right), Is.EqualTo(initial));

            Assert.That(left.TryQueueLane(0, 0), Is.True);
            string afterQueue = OnlineVersusProtocol.ComputeStateHash(left);
            Assert.That(afterQueue, Is.Not.EqualTo(initial));
            Assert.That(right.TryQueueLane(0, 0), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right), Is.EqualTo(afterQueue));

            Assert.That(left.TryCycleLanes(0), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(left), Is.Not.EqualTo(afterQueue));
            Assert.That(right.TryCycleLanes(0), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right),
                Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(left)));

            Assert.That(left.TryCommit(0), Is.True);
            Assert.That(right.TryCommit(0), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right),
                Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(left)));
        }

        [Test]
        public void StateHash_DistinguishesPlayerLoadoutsBeforeFirstAction()
        {
            var left = new VersusLoadout();
            var alternate = new VersusLoadout();
            Assert.That(alternate.TryPlaceSkill(alternate.GetSkillId(0, 1), 0, 0), Is.True);

            var defaultMatch = new LocalVersusMatch(left.ToSkills(), left.ToSkills(), randomSeed: 17);
            var customizedMatch = new LocalVersusMatch(left.ToSkills(), alternate.ToSkills(), randomSeed: 17);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(customizedMatch),
                Is.Not.EqualTo(OnlineVersusProtocol.ComputeStateHash(defaultMatch)));
        }

        [TestCase(LegacyStatusKind.PowerUp, 20, 2)]
        [TestCase(LegacyStatusKind.Protection, 25, 1)]
        [TestCase(LegacyStatusKind.Vulnerability, 50, 1)]
        [TestCase(LegacyStatusKind.Bleeding, 2, 3)]
        public void StateHash_IncludesPersistentStatuses(LegacyStatusKind kind, int percent, int count)
        {
            var first = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 17);
            var second = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 17);
            string initial = OnlineVersusProtocol.ComputeStateHash(first);
            Statuses(first.Left).Grant(kind, percent, count);
            Assert.That(first.Left.State.Health, Is.EqualTo(second.Left.State.Health));
            Assert.That(first.Left.Queue, Is.EqualTo(second.Left.Queue));
            Assert.That(OnlineVersusProtocol.ComputeStateHash(first), Is.Not.EqualTo(initial),
                "Equal health and reservations must not conceal different future damage modifiers.");
            Statuses(second.Left).Grant(kind, percent, count);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(first),
                Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(second)));
        }

        [Test]
        public void StateHash_DistinguishesStatusStrengthCountAndOwner()
        {
            var hashes = new System.Collections.Generic.HashSet<string>();
            foreach (int owner in new[] { 0, 1 })
                foreach (int percent in new[] { 25, 50 })
                    foreach (int count in new[] { 1, 2 })
                    {
                        var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, randomSeed: 17);
                        Statuses(owner == 0 ? match.Left : match.Right).Grant(LegacyStatusKind.Vulnerability, percent, count);
                        Assert.That(hashes.Add(OnlineVersusProtocol.ComputeStateHash(match)), Is.True);
                    }
        }

        [TestCase(0, true)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(1, false)]
        public void StateHash_IncludesEachOwnersActualActStolenAndReceivedFeedback(int owner, bool changeStolen)
        {
            LegacySkill steal = LegacySkillDefinitions.Skill(62);
            var match = new LocalVersusMatch(new[] { steal }, new[] { steal }, randomSeed: 17);
            for (int turn = 0; turn < 3; turn++)
            {
                while (match.Phase == LegacyDuelPhase.Planning)
                    Assert.That(match.TryCommit(match.CurrentPlanner), Is.True);
                match.BeginNextTurn();
            }
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int player = match.CurrentPlanner;
                Assert.That(match.TryQueueLane(player, 2), Is.True);
                Assert.That(match.TryCommit(player), Is.True);
            }
            LocalVersusSlot slot = match.BeginNextSlot();
            LegacySkillFeedback original = owner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            Assert.That(original.ActStolen, Is.Zero);
            Assert.That(original.ActReceived, Is.Zero);
            Assert.That(original.ActRecoveryStolen, Is.EqualTo(1));
            string before = OnlineVersusProtocol.ComputeStateHash(match);
            int leftAct = match.Left.Act, rightAct = match.Right.Act;

            // Isolate the digest's feedback input: replace only this immutable snapshot, keeping both wallets identical.
            var replacement = (LegacySkillFeedback)Activator.CreateInstance(typeof(LegacySkillFeedback),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                new object[] { steal, original.ConditionMet, original.EffectActivated, 0, 0, 0, 0, 0, 0, 0, 0, false,
                    original.ActStolen + (changeStolen ? 1 : 0), original.ActReceived + (changeStolen ? 0 : 1) }, null);
            replacement = (LegacySkillFeedback)typeof(LegacySkillFeedback).GetMethod("WithActRecoverySteal",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(replacement, new object[] { original.ActRecoveryStolen });
            System.Reflection.PropertyInfo property = typeof(LocalVersusSlot).GetProperty(owner == 0 ? "LeftFeedback" : "RightFeedback");
            property.SetValue(slot, replacement);
            Assert.That(match.Left.Act, Is.EqualTo(leftAct));
            Assert.That(match.Right.Act, Is.EqualTo(rightAct));
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(before));
            property.SetValue(slot, original);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void StateHash_IncludesEachOwnersRecoveryStealFeedbackAndSuccessfulUseState(int owner)
        {
            LegacySkill steal = LegacySkillDefinitions.Skill(62);
            var match = new LocalVersusMatch(new[] { steal }, new[] { steal });
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int player = match.CurrentPlanner;
                Assert.That(match.TryQueueLane(player, 2), Is.True);
                Assert.That(match.TryCommit(player), Is.True);
            }
            var slot = match.BeginNextSlot();
            var fighter = match.GetFighter(owner);
            var original = owner == 0 ? slot.LeftFeedback : slot.RightFeedback;
            Assert.That(original.ActRecoveryStolen, Is.EqualTo(1));
            string before = OnlineVersusProtocol.ComputeStateHash(match);
            var uses = (System.Collections.Generic.HashSet<int>)typeof(LocalVersusFighter).GetField("ActRecoveryStealUses",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fighter);
            Assert.That(uses.Remove(62), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(before));
            uses.Add(62);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(before));
            var replacement = (LegacySkillFeedback)typeof(LegacySkillFeedback).GetMethod("WithActRecoverySteal",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(original, new object[] { 2 });
            var property = typeof(LocalVersusSlot).GetProperty(owner == 0 ? "LeftFeedback" : "RightFeedback");
            property.SetValue(slot, replacement);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(before));
            property.SetValue(slot, original);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(before));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void StateHash_IncludesEachOwnersBleedingEligibilityConsumptionDamageAndFeedback(int owner)
        {
            LegacySkill Free(int id)
            {
                LegacySkill skill = LegacySkillDefinitions.Skill(id);
                return new LegacySkill(id, skill.Name, 0, 1, 1, skill.Kind, skill.Property, skill.AttackCount, 0, "");
            }
            var match = new LocalVersusMatch(new[] { Free(63) }, new[] { Free(64) }, randomSeed: 17);
            Statuses(match.Left).Grant(LegacyStatusKind.Bleeding, 2, 3);
            Statuses(match.Right).Grant(LegacyStatusKind.Bleeding, 2, 2);
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int planner = match.CurrentPlanner;
                Assert.That(match.TryQueueLane(planner, 0), Is.True);
                Assert.That(match.TryCommit(planner), Is.True);
            }
            LocalVersusSlot slot = match.BeginNextSlot();
            string before = OnlineVersusProtocol.ComputeStateHash(match);
            string prefix = owner == 0 ? "Left" : "Right";
            foreach (string suffix in new[] { "StartingBleeding", "BleedingDamagePerHit", "BleedingConsumed", "BleedingHealthDamage" })
            {
                var property = typeof(LocalVersusSlot).GetProperty(prefix + suffix);
                Assert.That(property, Is.Not.Null, suffix);
                object original = property.GetValue(slot);
                property.SetValue(slot, original is bool flag ? (object)!flag : (int)original + 1);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(before), prefix + suffix);
                property.SetValue(slot, original);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(before), prefix + suffix + " restored");
            }
            var feedbackProperty = typeof(LocalVersusSlot).GetProperty(prefix + "Feedback");
            var feedback = (LegacySkillFeedback)feedbackProperty.GetValue(slot);
            int[] values = { feedback.SelfBleedingGranted, feedback.OpponentBleedingGranted, feedback.BleedingRemoved,
                feedback.SelfBleedingPowerBonus, feedback.OpponentBleedingPowerBonus };
            var copy = typeof(LegacySkillFeedback).GetMethod("WithBleedingEffects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int field = 0; field < values.Length; field++)
            {
                object[] changed = { values[0], values[1], values[2], values[3], values[4] };
                changed[field] = values[field] + 1;
                feedbackProperty.SetValue(slot, copy.Invoke(feedback, changed));
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(before), "Bleeding feedback field " + field);
                feedbackProperty.SetValue(slot, feedback);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(before));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void StateHash_DistinguishesLaneReservationsEvenAfterReturnedQueuesBecomeIdentical(int player)
        {
            var attack = new LegacySkill(88101, "free", 0, 1, 1, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, string.Empty);
            var returned = new LegacySkill(32, "return", 0, 1, 1, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 1, string.Empty);
            var skills = new[] { attack, returned };
            var exhausted = new LocalVersusMatch(skills, skills, randomSeed: 17, openingPlayer: player);
            var untouched = new LocalVersusMatch(skills, skills, randomSeed: 17, openingPlayer: player);
            for (int count = 0; count < LegacyLaneLimit.MaxReservations; count++)
                Assert.That(exhausted.TryQueueLane(player, 0), Is.True);
            Assert.That(exhausted.TryQueueLane(player, 1) && untouched.TryQueueLane(player, 1), Is.True);
            LocalVersusFighter first = exhausted.GetFighter(player), second = untouched.GetFighter(player);
            Assert.That(first.Queue, Is.EqualTo(second.Queue));
            Assert.That(first.GetLane(0), Is.EqualTo(second.GetLane(0)));
            Assert.That(first.Act, Is.EqualTo(second.Act));
            Assert.That((first.LaneReservationsRemaining(0), second.LaneReservationsRemaining(0)), Is.EqualTo((0, 3)));
            string exhaustedHash = OnlineVersusProtocol.ComputeStateHash(exhausted);
            Assert.That(exhaustedHash, Is.Not.EqualTo(OnlineVersusProtocol.ComputeStateHash(untouched)),
                "Equal current queues must not conceal different legal next actions for either peer.");
            Assert.That(exhausted.ForecastIfQueued(player, 0), Is.Null);
            Assert.That(untouched.ForecastIfQueued(player, 0), Is.Not.Null);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(exhausted), Is.EqualTo(exhaustedHash));
            Assert.That(exhausted.TryQueueLane(player, 0), Is.False);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(exhausted), Is.EqualTo(exhaustedHash),
                "A rejected reservation cannot change the authoritative state.");
        }

        private static LegacyStatusCollection Statuses(LocalVersusFighter fighter)
            => (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fighter);

        private static string ReferenceRulesHash(byte[] sheet, string gameVersion, string combatVersion)
        {
            const ulong prime = 1099511628211UL;
            ulong hash = 14695981039346656037UL;
            void Byte(byte value)
            {
                unchecked { hash = (hash ^ value) * prime; }
            }
            void Integer(int value)
            {
                uint bits = unchecked((uint)value);
                for (int shift = 0; shift < 32; shift += 8) Byte((byte)(bits >> shift));
            }
            void String(string value)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
                Integer(bytes.Length);
                foreach (byte item in bytes) Byte(item);
            }
            String(combatVersion);
            String(gameVersion);
            byte[] normalized = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Encoding.UTF8.GetString(sheet).Replace("\r\n", "\n").Replace("\r", "\n"));
            Integer(normalized.Length);
            foreach (byte item in normalized) Byte(item);
            return hash.ToString("x16");
        }

        private static void AssertRejected(string json)
        {
            Assert.That(OnlineVersusMessage.TryDecode(json, out OnlineVersusMessage message, out string error),
                Is.False, "Payload should be rejected: " + json.Substring(0, Math.Min(60, json.Length)));
            Assert.That(message, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
