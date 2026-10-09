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
        public void Decoder_RejectsMalformedOversizedUnsupportedAndOutOfRangeMessages()
        {
            AssertRejected(string.Empty);
            AssertRejected("{}");
            AssertRejected("{bad json}");
            AssertRejected(new string('x', OnlineVersusMessage.MaximumPayloadBytes + 1));

            var invalid = new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Hello };
            invalid.ProtocolVersion = 2;
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

            Assert.That(left.TryCycleLanes(1), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(left), Is.Not.EqualTo(afterQueue));
            Assert.That(right.TryCycleLanes(1), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right),
                Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(left)));

            Assert.That(left.TryPass(1), Is.True);
            Assert.That(right.TryPass(1), Is.True);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(right),
                Is.EqualTo(OnlineVersusProtocol.ComputeStateHash(left)));
        }

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
            Integer(sheet.Length);
            foreach (byte item in sheet) Byte(item);
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
