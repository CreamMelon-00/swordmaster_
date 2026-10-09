using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TurnLimbo.Presentation
{
    /// <summary>Invite lobby and host-authoritative command stream for a two-player duel.</summary>
    public sealed class OnlineVersusController : IDisposable
    {
        private readonly LocalVersusController duel;
        private readonly OnlineInviteHud hud;
        private readonly VersusLoadoutHud loadoutHud;
        private readonly VersusLoadout localLoadout;
        private VersusLoadout remoteLoadout;
        private readonly OnlineRelayLink link;
        private readonly Action returnToTitle;
        private readonly Queue<OnlineVersusMessage> accepted = new Queue<OnlineVersusMessage>();
        private readonly Queue<OnlineVersusMessage> digests = new Queue<OnlineVersusMessage>();
        private readonly string rulesHash;
        private Task connectionTask = Task.CompletedTask;
        private Task leaveTask = Task.CompletedTask;
        private OnlineInvitePhase phase = OnlineInvitePhase.Choice;
        private string message;
        private string pendingFailure;
        private string matchId;
        private bool active, battle, busy, host, peerConnected, rulesMatched, localReady, remoteReady;
        private bool repliedToHello;
        private bool localRematch, remoteRematch, sentFinalDigest, disposed, previousRunInBackground;
        private int generation, nextRequestSequence, lastRequestSequence, nextAppliedSequence, lastAppliedSequence;
        private int lastDigestRound, lastDigestSequence, rematchCount;
        private float clockBroadcast, handshakeElapsed, handshakeRetryElapsed;

        public bool IsActive => active;
        public bool IsInBattle => active && battle;
        public OnlineInviteHud Hud => hud;

        public OnlineVersusController(Transform parent, LegacyDuelArt art, LocalVersusController duel,
            Action returnToTitle)
        {
            this.duel = duel ?? throw new ArgumentNullException(nameof(duel));
            this.returnToTitle = returnToTitle ?? throw new ArgumentNullException(nameof(returnToTitle));
            link = new OnlineRelayLink();
            link.PeerJoined += OnPeerJoined;
            link.PeerLeft += OnPeerLeft;
            link.MessageReceived += OnMessage;
            duel.OnlineActionApplied += OnHostActionApplied;
            localLoadout = new VersusLoadout();
            hud = new OnlineInviteHud(parent, art, Create, Join, Ready, StartBattle, LeaveToTitle);
            hud.ConfigureSkills = ConfigureSkills;
            loadoutHud = new VersusLoadoutHud(parent, art, localLoadout,
                CloseLoadout, CloseLoadout, Refresh);
            loadoutHud.Hide();
            rulesHash = OnlineVersusProtocol.ComputeRulesHash();
        }

        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OnlineVersusController));
            if (active) return;
            generation++;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            active = true;
            battle = busy = host = peerConnected = rulesMatched = localReady = remoteReady = false;
            localRematch = remoteRematch = false;
            remoteLoadout = null;
            rematchCount = 0;
            phase = OnlineInvitePhase.Choice;
            message = matchId = pendingFailure = null;
            hud.Show();
            loadoutHud.Hide();
            Refresh();
        }

        public void Stop()
        {
            if (!active) return;
            generation++;
            bool wasConnecting = busy;
            active = battle = busy = peerConnected = rulesMatched = false;
            Application.runInBackground = previousRunInBackground;
            hud.Hide();
            loadoutHud.Hide();
            duel.Stop();
            accepted.Clear();
            digests.Clear();
            remoteLoadout = null;
            // A connection in progress closes itself after the service call completes.
            leaveTask = wasConnecting ? connectionTask : LeaveSafelyAsync();
        }

        public void LeaveToTitle()
        {
            if (active) returnToTitle();
        }

        public void Tick(float delta, Keyboard keyboard)
        {
            if (!active) return;
            if (!string.IsNullOrEmpty(pendingFailure))
            {
                Fail(pendingFailure);
                return;
            }
            if (!battle)
            {
                if (peerConnected && !rulesMatched)
                {
                    handshakeElapsed += Mathf.Max(0f, delta);
                    handshakeRetryElapsed += Mathf.Max(0f, delta);
                    if (handshakeRetryElapsed >= 1f)
                    {
                        handshakeRetryElapsed = 0f;
                        Send(new OnlineVersusMessage {
                            Kind = OnlineVersusMessageKind.Hello, RulesHash = rulesHash
                        });
                    }
                    if (handshakeElapsed >= 15f)
                    {
                        Fail("친구와 게임 버전을 확인하지 못했습니다. 다시 연결해 주세요.");
                        return;
                    }
                }
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (loadoutHud.IsVisible) loadoutHud.Cancel();
                    else LeaveToTitle();
                }
                return;
            }
            duel.Tick(delta, keyboard);
            if (!string.IsNullOrEmpty(pendingFailure))
            {
                Fail(pendingFailure);
                return;
            }
            if (!active || !battle || duel.Match == null) return;
            if (host)
            {
                clockBroadcast += Mathf.Max(0f, delta);
                if (clockBroadcast >= .5f)
                {
                    clockBroadcast = 0f;
                    Send(new OnlineVersusMessage {
                        Kind = OnlineVersusMessageKind.Clock, MatchId = matchId,
                        Round = duel.Match.RoundNumber, LeftClock = duel.LeftClock, RightClock = duel.RightClock
                    });
                }
                SendDigestAtTurnBoundary();
                if (localRematch && remoteRematch && duel.Match.IsFinished) TryBeginRematch();
            }
            else
            {
                ApplyAcceptedInOrder();
                CheckDigests();
            }
        }

        private void Create() => QueueConnection(true, null);
        private void Join(string code) => QueueConnection(false, code);

        private void QueueConnection(bool create, string code)
        {
            if (!active || busy || battle || phase == OnlineInvitePhase.Lobby) return;
            busy = true;
            host = create;
            phase = OnlineInvitePhase.Connecting;
            message = null;
            Refresh();
            Task previous = connectionTask;
            Task previousLeave = leaveTask;
            connectionTask = ConnectAsync(previous, previousLeave, create, code, generation);
        }

        private async Task ConnectAsync(Task previous, Task previousLeave, bool create, string code, int attempt)
        {
            try
            {
                await previous;
                await previousLeave;
                if (!active || generation != attempt) return;
                if (create) await link.HostAsync();
                else await link.JoinAsync(code);
                if (!active || generation != attempt)
                {
                    await LeaveSafelyAsync();
                    return;
                }
                busy = false;
                phase = OnlineInvitePhase.Lobby;
                if (!create && !peerConnected && link.IsConnected) OnPeerJoined();
                Refresh();
            }
            catch (Exception exception)
            {
                if (active && generation == attempt) Fail(exception.Message);
            }
        }

        private void OnPeerJoined()
        {
            if (!active) return;
            peerConnected = true;
            handshakeElapsed = handshakeRetryElapsed = 0f;
            repliedToHello = false;
            rulesMatched = localReady = remoteReady = false;
            remoteLoadout = null;
            Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Hello, RulesHash = rulesHash });
            Refresh();
        }

        private void OnPeerLeft()
        {
            if (!active) return;
            peerConnected = rulesMatched = localReady = remoteReady = false;
            remoteLoadout = null;
            if (battle)
            {
                Fail("친구와 연결이 끊어져 대전을 종료했습니다.");
                return;
            }
            message = host ? "친구가 방에서 나갔습니다. 새 친구를 기다립니다." : "방과 연결이 끊어졌습니다.";
            if (!host) Fail(message);
            else Refresh();
        }

        private void OnMessage(string json)
        {
            if (!active) return;
            if (!OnlineVersusMessage.TryDecode(json, out OnlineVersusMessage packet, out string error))
            {
                Fail("친구와 대전 메시지 버전이 맞지 않습니다: " + error);
                return;
            }
            try
            {
                switch (packet.Kind)
                {
                    case OnlineVersusMessageKind.Hello: ReceiveHello(packet); break;
                    case OnlineVersusMessageKind.Ready: ReceiveReady(packet); break;
                    case OnlineVersusMessageKind.Start: ReceiveStart(packet); break;
                    case OnlineVersusMessageKind.Request: ReceiveRequest(packet); break;
                    case OnlineVersusMessageKind.Applied: ReceiveApplied(packet); break;
                    case OnlineVersusMessageKind.Clock: ReceiveClock(packet); break;
                    case OnlineVersusMessageKind.TurnDigest: ReceiveDigest(packet); break;
                    case OnlineVersusMessageKind.Rematch: ReceiveRematch(packet); break;
                    case OnlineVersusMessageKind.Error: ReceiveError(packet); break;
                }
            }
            catch (Exception exception)
            {
                Fail("대전 상태를 맞추지 못했습니다: " + exception.Message);
            }
        }

        private void ReceiveHello(OnlineVersusMessage packet)
        {
            if (battle || !peerConnected) return;
            if (!string.Equals(packet.RulesHash, rulesHash, StringComparison.Ordinal))
            {
                Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Error,
                    Text = "게임 데이터 버전이 달라 대전할 수 없습니다." });
                Fail("친구와 게임 데이터 버전이 다릅니다. 같은 빌드를 사용해 주세요.");
                return;
            }
            rulesMatched = true;
            // Either side can announce Hello before its peer has installed a message handler.
            // One acknowledgement from each side makes that race harmless without an echo loop.
            if (!repliedToHello)
            {
                repliedToHello = true;
                Send(new OnlineVersusMessage {
                    Kind = OnlineVersusMessageKind.Hello, RulesHash = rulesHash
                });
            }
            message = null;
            Refresh();
        }

        private void ConfigureSkills()
        {
            if (!active || battle || busy || localReady || phase != OnlineInvitePhase.Lobby) return;
            loadoutHud.Show();
        }

        private void CloseLoadout()
        {
            loadoutHud.Hide();
            Refresh();
        }

        private void Ready()
        {
            if (!active || battle || busy || !peerConnected || !rulesMatched || localReady) return;
            loadoutHud.Hide();
            localReady = true;
            if (host) BroadcastReady();
            else Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Ready,
                Player = 1, RightReady = true, RightSkillIds = localLoadout.ExportIds() });
            Refresh();
        }

        private void ReceiveReady(OnlineVersusMessage packet)
        {
            if (battle || !peerConnected || !rulesMatched) return;
            if (host)
            {
                if (packet.Player != 1 || !packet.RightReady) return;
                var candidate = new VersusLoadout();
                if (!candidate.TryImportIds(packet.RightSkillIds) ||
                    (remoteReady && remoteLoadout != null &&
                     !SameIds(remoteLoadout.ExportIds(), packet.RightSkillIds)))
                {
                    Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Error,
                        Text = "친구의 기술 편성을 확인할 수 없습니다." });
                    Fail("친구의 기술 편성이 올바르지 않습니다.");
                    return;
                }
                remoteLoadout = candidate;
                remoteReady = true;
                BroadcastReady();
            }
            else
            {
                // Readiness is owned locally. A delayed host echo must not unlock the draft.
                remoteReady = packet.LeftReady;
            }
            Refresh();
        }

        private static bool SameIds(int[] expected, int[] actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i]) return false;
            return true;
        }

        private void BroadcastReady()
        {
            Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Ready,
                LeftReady = localReady, RightReady = remoteReady });
        }

        private void StartBattle()
        {
            if (!active || battle || !host || !peerConnected || !rulesMatched || !localReady || !remoteReady)
                return;
            StartHostBattle();
        }

        private void StartHostBattle()
        {
            if (remoteLoadout == null) return;
            int seed = Guid.NewGuid().GetHashCode();
            int opening = rematchCount % 2;
            string id = Guid.NewGuid().ToString("N");
            if (!Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Start,
                MatchId = id, Seed = seed, OpeningPlayer = opening, RulesHash = rulesHash,
                LeftSkillIds = localLoadout.ExportIds(), RightSkillIds = remoteLoadout.ExportIds() }))
            {
                if (!string.IsNullOrEmpty(pendingFailure)) Fail(pendingFailure);
                return;
            }
            if (active && peerConnected) BeginBattle(id, seed, opening, true,
                localLoadout, remoteLoadout);
        }

        private void ReceiveStart(OnlineVersusMessage packet)
        {
            if (host || !peerConnected || !rulesMatched || !localReady ||
                !string.Equals(packet.RulesHash, rulesHash, StringComparison.Ordinal)) return;
            if (battle && (duel.Match == null || !duel.Match.IsFinished)) return;
            var left = new VersusLoadout();
            var right = new VersusLoadout();
            if (!left.TryImportIds(packet.LeftSkillIds) || !right.TryImportIds(packet.RightSkillIds) ||
                !SameIds(localLoadout.ExportIds(), packet.RightSkillIds))
            {
                Fail("친구와 기술 편성이 일치하지 않습니다. 다시 방을 만들어 주세요.");
                return;
            }
            remoteLoadout = left;
            BeginBattle(packet.MatchId, packet.Seed, packet.OpeningPlayer, false, left, right);
        }

        private void BeginBattle(string id, int seed, int opening, bool isHost,
            VersusLoadout left, VersusLoadout right)
        {
            matchId = id;
            battle = true;
            localRematch = remoteRematch = sentFinalDigest = false;
            nextRequestSequence = lastRequestSequence = nextAppliedSequence = lastAppliedSequence = 0;
            lastDigestSequence = 0;
            lastDigestRound = 1;
            clockBroadcast = 0f;
            accepted.Clear();
            digests.Clear();
            hud.Hide();
            loadoutHud.Hide();
            duel.StartOnline(seed, isHost ? 0 : 1, isHost, opening,
                left.ToSkills(), right.ToSkills(), RequestAction, RequestRematch);
        }

        private void RequestAction(OnlineVersusAction action, int lane)
        {
            if (!battle || host || duel.Match == null) return;
            Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Request,
                MatchId = matchId, Sequence = ++nextRequestSequence,
                Round = duel.Match.RoundNumber, Player = 1, Action = action, Lane = lane });
        }

        private void ReceiveRequest(OnlineVersusMessage packet)
        {
            if (!host || !battle || packet.MatchId != matchId || packet.Player != 1) return;
            if (packet.Sequence <= lastRequestSequence) return;
            if (packet.Sequence != lastRequestSequence + 1)
            {
                Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Error,
                    MatchId = matchId, Text = "명령 순서가 어긋났습니다." });
                Fail("명령 순서가 어긋났습니다.");
                return;
            }
            lastRequestSequence = packet.Sequence;
            if (!duel.TryApplyOnlinePeerAction(packet.Action, packet.Lane, packet.Round))
                Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Error,
                    MatchId = matchId, Sequence = packet.Sequence,
                    Text = "예약할 수 없는 행동입니다." });
        }

        private void OnHostActionApplied(int player, OnlineVersusAction action, int lane, int round)
        {
            if (!host || !battle || duel.Match == null) return;
            Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Applied,
                MatchId = matchId, Sequence = ++nextAppliedSequence,
                Round = round, Player = player, Action = action, Lane = lane,
                LeftClock = duel.LeftClock, RightClock = duel.RightClock,
                StateHash = OnlineVersusProtocol.ComputeStateHash(duel.Match) });
        }

        private void ReceiveApplied(OnlineVersusMessage packet)
        {
            if (host || !battle || packet.MatchId != matchId) return;
            if (packet.Sequence <= lastAppliedSequence + accepted.Count) return;
            if (packet.Sequence != lastAppliedSequence + accepted.Count + 1)
            {
                Fail("친구와 대전 명령 순서가 어긋났습니다.");
                return;
            }
            accepted.Enqueue(packet);
            ApplyAcceptedInOrder();
        }

        private void ApplyAcceptedInOrder()
        {
            if (host || !battle || duel.Match == null) return;
            while (accepted.Count > 0)
            {
                OnlineVersusMessage packet = accepted.Peek();
                if (packet.Round > duel.Match.RoundNumber) return;
                if (packet.Round < duel.Match.RoundNumber)
                {
                    Fail("대전 명령의 턴 순서가 어긋났습니다.");
                    return;
                }
                if (duel.Match.Phase != LegacyDuelPhase.Planning) return;
                if (!duel.ApplyOnlineAcceptedAction(packet.Player, packet.Action, packet.Lane, packet.Round))
                {
                    Fail("친구와 기술 예약 상태가 달라졌습니다.");
                    return;
                }
                accepted.Dequeue();
                lastAppliedSequence = packet.Sequence;
                duel.SyncOnlineClocks(packet.LeftClock, packet.RightClock);
                if (!string.Equals(packet.StateHash,
                    OnlineVersusProtocol.ComputeStateHash(duel.Match), StringComparison.Ordinal))
                {
                    Fail("친구와 대전 상태가 달라졌습니다. 같은 빌드로 다시 시도해 주세요.");
                    return;
                }
            }
        }

        private void ReceiveClock(OnlineVersusMessage packet)
        {
            if (host || !battle || packet.MatchId != matchId || duel.Match == null ||
                packet.Round != duel.Match.RoundNumber) return;
            duel.SyncOnlineClocks(packet.LeftClock, packet.RightClock);
        }

        private void SendDigestAtTurnBoundary()
        {
            if (duel.Match == null) return;
            int round = duel.Match.RoundNumber;
            bool finished = duel.Match.IsFinished && duel.Match.CurrentSlot == null;
            if (round == lastDigestRound && (!finished || sentFinalDigest)) return;
            lastDigestRound = round;
            if (finished) sentFinalDigest = true;
            Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.TurnDigest,
                MatchId = matchId, Sequence = ++lastDigestSequence, Round = round,
                StateHash = OnlineVersusProtocol.ComputeStateHash(duel.Match),
                Text = finished ? "finished" : "planning" });
        }

        private void ReceiveDigest(OnlineVersusMessage packet)
        {
            if (host || !battle || packet.MatchId != matchId) return;
            if (packet.Sequence <= lastDigestSequence + digests.Count) return;
            if (packet.Sequence != lastDigestSequence + digests.Count + 1)
            {
                Fail("친구와 턴 결과 순서가 어긋났습니다.");
                return;
            }
            digests.Enqueue(packet);
            CheckDigests();
        }

        private void CheckDigests()
        {
            if (duel.Match == null) return;
            while (digests.Count > 0)
            {
                OnlineVersusMessage packet = digests.Peek();
                if (duel.Match.RoundNumber < packet.Round) return;
                if (duel.Match.RoundNumber > packet.Round)
                {
                    Fail("턴 결과가 서로 달라졌습니다.");
                    return;
                }
                bool final = packet.Text == "finished";
                if (final ? !duel.Match.IsFinished || duel.Match.CurrentSlot != null
                    : duel.Match.Phase != LegacyDuelPhase.Planning) return;
                if (!string.Equals(packet.StateHash,
                    OnlineVersusProtocol.ComputeStateHash(duel.Match), StringComparison.Ordinal))
                {
                    Fail("친구와 턴 결과가 달라졌습니다. 다시 대전해 주세요.");
                    return;
                }
                digests.Dequeue();
                lastDigestSequence = packet.Sequence;
            }
        }

        private void RequestRematch()
        {
            if (!battle || duel.Match == null || !duel.Match.IsFinished) return;
            localRematch = true;
            if (host) TryBeginRematch();
            else Send(new OnlineVersusMessage { Kind = OnlineVersusMessageKind.Rematch,
                MatchId = matchId, Player = 1 });
        }

        private void ReceiveRematch(OnlineVersusMessage packet)
        {
            if (!host || !battle || packet.MatchId != matchId || packet.Player != 1 ||
                duel.Match == null) return;
            // The guest can finish its presentation before the host. Keep its vote until
            // this machine reaches the same result.
            remoteRematch = true;
            TryBeginRematch();
        }

        private void TryBeginRematch()
        {
            if (!host || !localRematch || !remoteRematch || !peerConnected ||
                duel.Match == null || !duel.Match.IsFinished) return;
            rematchCount++;
            StartHostBattle();
        }

        private void ReceiveError(OnlineVersusMessage packet)
        {
            if (battle && !string.IsNullOrEmpty(packet.MatchId) && packet.MatchId != matchId) return;
            if (packet.MatchId == matchId && battle && !host &&
                packet.Text == "예약할 수 없는 행동입니다.")
            {
                if (packet.Sequence == nextRequestSequence) duel.RejectOnlineRequest();
                return;
            }
            Fail(string.IsNullOrEmpty(packet.Text) ? "온라인 대전을 진행할 수 없습니다." : packet.Text);
        }

        private bool Send(OnlineVersusMessage packet)
        {
            if (!active || !peerConnected) return false;
            try
            {
                link.Send(packet.Encode());
                return true;
            }
            catch (Exception exception)
            {
                // A send can fail inside LocalVersusController's action callback. End the
                // online session after that callback returns to avoid clearing its match mid-frame.
                pendingFailure = "연결이 끊어졌습니다: " + exception.Message;
                return false;
            }
        }

        private void Fail(string reason)
        {
            if (!active) return;
            generation++;
            pendingFailure = null;
            duel.Stop();
            battle = busy = peerConnected = rulesMatched = localReady = remoteReady = false;
            accepted.Clear();
            digests.Clear();
            remoteLoadout = null;
            loadoutHud.Hide();
            phase = OnlineInvitePhase.Error;
            message = string.IsNullOrWhiteSpace(reason) ? "온라인 대전을 시작할 수 없습니다." : reason;
            hud.Show();
            Refresh();
            leaveTask = LeaveSafelyAsync();
        }

        private async Task LeaveSafelyAsync()
        {
            try { await link.LeaveAsync(); }
            catch (Exception exception) { Debug.LogWarning("온라인 대전 연결 종료 실패: " + exception.Message); }
        }

        private void Refresh()
        {
            if (!active) return;
            hud.Refresh(new OnlineInviteView {
                Phase = phase, Host = host, PeerConnected = peerConnected && rulesMatched,
                LocalReady = localReady, RemoteReady = remoteReady, Busy = busy,
                Code = link.Code, Message = message
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            duel.OnlineActionApplied -= OnHostActionApplied;
            link.PeerJoined -= OnPeerJoined;
            link.PeerLeft -= OnPeerLeft;
            link.MessageReceived -= OnMessage;
            loadoutHud.Dispose();
            hud.Dispose();
            link.Dispose();
        }
    }
}

