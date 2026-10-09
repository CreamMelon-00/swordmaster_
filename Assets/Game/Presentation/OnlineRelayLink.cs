using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Two-player, private Relay connection. Gameplay authority and message format belong to the caller.</summary>
    public sealed class OnlineRelayLink : IDisposable
    {
        private const string MessageName = "TurnLimbo.Versus.Command.v1";
        private const int MaxMessageCharacters = 8192;
        private static Task retiringNetworkCleanup = Task.CompletedTask;

        private GameObject networkRoot;
        private NetworkManager network;
        private ISession session;
        private ulong? remoteClientId;
        private string code;
        private bool hosting;
        private bool leaving;
        private bool disposed;
        private bool handlerRegistered;
        private bool peerEventPending;
        private bool connecting;
        private bool closingRequested;
        private Task connectionTask = Task.CompletedTask;
        private Task leaveTask = Task.CompletedTask;

        public event Action PeerJoined;
        public event Action PeerLeft;
        public event Action<string> MessageReceived;

        public bool IsHost => session?.IsHost ?? hosting;
        public bool IsConnected => session != null && network != null && network.IsConnectedClient;
        public string Code => code;

        public Task<string> HostAsync()
        {
            EnsureCanStart();
            if (string.IsNullOrEmpty(Application.cloudProjectId))
                throw new InvalidOperationException("Unity Cloud 프로젝트를 연결해야 온라인 대전을 열 수 있습니다.");

            connecting = true;
            hosting = true;
            Task<string> task = HostCoreAsync();
            connectionTask = task;
            return task;
        }

        private async Task<string> HostCoreAsync()
        {
            try
            {
                await AuthenticateAsync();
                ThrowIfClosing();
                await WaitForPreviousNetworkManagerAsync();
                ThrowIfClosing();
                CreateNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(
                    new SessionOptions { MaxPlayers = 2, IsPrivate = true }.WithRelayNetwork());
                ThrowIfClosing();
                code = session.Code;
                RegisterMessageHandler();
                CaptureExistingPeer();
                return code;
            }
            catch
            {
                try { await CloseSessionAsync(); }
                catch (Exception exception) { Debug.LogWarning("온라인 대전 연결 정리 실패: " + exception.Message); }
                throw;
            }
            finally { connecting = false; }
        }

        public Task JoinAsync(string joinCode)
        {
            EnsureCanStart();
            if (string.IsNullOrEmpty(Application.cloudProjectId))
                throw new InvalidOperationException("Unity Cloud 프로젝트를 연결해야 온라인 대전에 참가할 수 있습니다.");

            joinCode = joinCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(joinCode))
                throw new ArgumentException("초대 코드를 입력하세요.", nameof(joinCode));

            connecting = true;
            hosting = false;
            Task task = JoinCoreAsync(joinCode);
            connectionTask = task;
            return task;
        }

        private async Task JoinCoreAsync(string joinCode)
        {
            try
            {
                await AuthenticateAsync();
                ThrowIfClosing();
                await WaitForPreviousNetworkManagerAsync();
                ThrowIfClosing();
                CreateNetwork();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);
                ThrowIfClosing();
                code = session.Code;
                RegisterMessageHandler();
                CaptureExistingPeer();
            }
            catch
            {
                try { await CloseSessionAsync(); }
                catch (Exception exception) { Debug.LogWarning("온라인 대전 연결 정리 실패: " + exception.Message); }
                throw;
            }
            finally { connecting = false; }
        }

        public Task LeaveAsync()
        {
            if (leaving) return leaveTask;
            leaving = true;
            closingRequested = true;
            leaveTask = LeaveCoreAsync();
            return leaveTask;
        }

        private async Task LeaveCoreAsync()
        {
            try
            {
                // MPS can still be starting NGO while authentication or session creation is in flight.
                try { await connectionTask; }
                catch { /* The pending start's own cleanup handles its error. */ }
                await CloseSessionAsync();
            }
            finally
            {
                leaving = false;
                closingRequested = false;
            }
        }

        private async Task CloseSessionAsync()
        {
            ISession departing = session;
            session = null;
            code = null;
            remoteClientId = null;
            peerEventPending = false;
            try
            {
                if (departing != null) await departing.LeaveAsync();
            }
            finally
            {
                if (network != null)
                {
                    if (handlerRegistered && network.CustomMessagingManager != null)
                        network.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                    if (network.IsListening && !network.ShutdownInProgress) network.Shutdown();
                    float shutdownDeadline = Time.realtimeSinceStartup + 5f;
                    while (network.ShutdownInProgress && Time.realtimeSinceStartup < shutdownDeadline)
                        await Task.Yield();
                }
                handlerRegistered = false;
                hosting = false;
            }
        }

        private void ThrowIfClosing()
        {
            if (closingRequested || disposed)
                throw new OperationCanceledException("온라인 대전 연결이 취소되었습니다.");
        }

        private static async Task WaitForPreviousNetworkManagerAsync()
        {
            // Dispose returns immediately, while MPS leave and Unity's Destroy finish later.
            // Wait for that cleanup before asking NGO to start another session.
            await retiringNetworkCleanup;
        }

        public void Send(string json)
        {
            if (!IsConnected || !remoteClientId.HasValue)
                throw new InvalidOperationException("상대가 연결되어 있지 않습니다.");
            if (string.IsNullOrEmpty(json) || json.Length > MaxMessageCharacters)
                throw new ArgumentException("대전 메시지의 길이가 올바르지 않습니다.", nameof(json));

            // FastBufferWriter's string encoding uses two bytes per character by default.
            using (var writer = new FastBufferWriter(checked(json.Length * 2 + 16), Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                network.CustomMessagingManager.SendNamedMessage(
                    MessageName, remoteClientId.Value, writer, NetworkDelivery.ReliableSequenced);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var cleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            retiringNetworkCleanup = Task.WhenAll(retiringNetworkCleanup, cleanup.Task);
            _ = LeaveOnDisposeAsync(cleanup);
        }

        private async Task LeaveOnDisposeAsync(TaskCompletionSource<bool> cleanup)
        {
            try { await LeaveAsync(); }
            catch (Exception exception) { Debug.LogWarning("온라인 대전 연결 종료 실패: " + exception.Message); }
            finally
            {
                try
                {
                    NetworkManager departingNetwork = network;
                    DestroyNetwork();
                    if (departingNetwork != null)
                    {
                        float deadline = Time.realtimeSinceStartup + 5f;
                        while (ReferenceEquals(NetworkManager.Singleton, departingNetwork) &&
                               Time.realtimeSinceStartup < deadline)
                            await Task.Yield();
                    }
                }
                finally { cleanup.TrySetResult(true); }
            }
        }

        private void EnsureCanStart()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OnlineRelayLink));
            if (session != null || connecting || leaving)
                throw new InvalidOperationException("이미 온라인 대전 연결이 진행 중입니다.");
        }

        private static async Task AuthenticateAsync()
        {
            string profile = CommandLineProfile();
            if (profile == null) await UnityServices.InitializeAsync();
            else
            {
                var options = new InitializationOptions();
                options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string CommandLineProfile()
        {
            const string prefix = "--online-profile=";
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (!argument.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string value = argument.Substring(prefix.Length);
                if (value.Length < 1 || value.Length > 30)
                    throw new ArgumentException("온라인 인증 프로필은 1~30자여야 합니다.");
                foreach (char c in value)
                    if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                          (c >= '0' && c <= '9') || c == '-' || c == '_'))
                        throw new ArgumentException("온라인 인증 프로필에는 영문자, 숫자, -와 _만 사용할 수 있습니다.");
                return value;
            }
            return null;
        }

        private void CreateNetwork()
        {
            // Reuse the same NGO component across sequential rooms. Destroy is deferred until
            // frame end, so disposing one link and creating another must await the cleanup task.
            if (network != null)
            {
                if (NetworkManager.Singleton != network || network.ShutdownInProgress)
                    throw new InvalidOperationException("이전 온라인 대전 연결이 아직 종료 중입니다.");
                return;
            }
            if (NetworkManager.Singleton != null)
                throw new InvalidOperationException("기존 Netcode NetworkManager가 있어 대전 연결을 만들 수 없습니다.");

            networkRoot = new GameObject("Online Versus Network");
            networkRoot.SetActive(false);
            var transport = networkRoot.AddComponent<UnityTransport>();
            network = networkRoot.AddComponent<NetworkManager>();
            // Runtime AddComponent does not run the editor's NetworkManager reset initializer.
            if (network.NetworkConfig == null) network.NetworkConfig = new NetworkConfig();
            network.NetworkConfig.NetworkTransport = transport;
            network.NetworkConfig.EnableSceneManagement = false;
            network.NetworkConfig.PlayerPrefab = null;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            networkRoot.SetActive(true);
        }

        private void DestroyNetwork()
        {
            if (network != null)
            {
                network.OnClientConnectedCallback -= OnClientConnected;
                network.OnClientDisconnectCallback -= OnClientDisconnected;
                if (handlerRegistered && network.CustomMessagingManager != null)
                    network.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                if (network.IsListening) network.Shutdown();
            }
            if (networkRoot != null) UnityEngine.Object.Destroy(networkRoot);
            network = null;
            networkRoot = null;
            handlerRegistered = false;
        }

        private void RegisterMessageHandler()
        {
            if (handlerRegistered || network?.CustomMessagingManager == null) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            handlerRegistered = true;
        }

        private void CaptureExistingPeer()
        {
            if (peerEventPending)
            {
                peerEventPending = false;
                PeerJoined?.Invoke();
                return;
            }
            if (network == null || !network.IsConnectedClient) return;
            if (hosting)
            {
                foreach (ulong clientId in network.ConnectedClientsIds)
                {
                    if (clientId == network.LocalClientId) continue;
                    MarkPeerJoined(clientId);
                    break;
                }
            }
            else MarkPeerJoined(NetworkManager.ServerClientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (network == null || leaving || closingRequested) return;
            RegisterMessageHandler();
            if (hosting)
            {
                if (clientId != network.LocalClientId) MarkPeerJoined(clientId);
            }
            else if (clientId == network.LocalClientId)
                MarkPeerJoined(NetworkManager.ServerClientId);
        }

        private void MarkPeerJoined(ulong clientId)
        {
            if (remoteClientId.HasValue) return;
            remoteClientId = clientId;
            if (session == null) peerEventPending = true;
            else PeerJoined?.Invoke();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (network == null || leaving || !remoteClientId.HasValue) return;
            if (peerEventPending)
            {
                peerEventPending = false;
                remoteClientId = null;
                return;
            }
            if ((hosting && clientId != network.LocalClientId && clientId == remoteClientId.Value) ||
                (!hosting && (clientId == network.LocalClientId || clientId == NetworkManager.ServerClientId)))
            {
                remoteClientId = null;
                PeerLeft?.Invoke();
            }
        }

        private void OnMessage(ulong senderId, FastBufferReader reader)
        {
            if (session == null || !remoteClientId.HasValue || senderId != remoteClientId.Value) return;
            try
            {
                reader.ReadValueSafe(out string message);
                if (!string.IsNullOrEmpty(message) && message.Length <= MaxMessageCharacters)
                    MessageReceived?.Invoke(message);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("온라인 대전 메시지를 읽지 못했습니다: " + exception.Message);
            }
        }
    }
}
