using Sirenix.OdinInspector;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode.Transports.SinglePlayer;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;


namespace SketchLand
{
    public class Relay : MonoBehaviour
    {
        [Header("Playtest Mode")]
        [Tooltip("Run locally without Unity Services, Relay, or network sockets. Turn off to use the existing multiplayer flow. This setting also applies to builds.")]
        [SerializeField] private bool _singleplayer = true;

        private bool _createdRelay;
        private bool _joinedRelay;
        private bool _isOffline;

        private async void Start()
        {
            if (_singleplayer) return;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;

            // Detect offline mode
            _isOffline = Application.internetReachability == NetworkReachability.NotReachable;

            if (_isOffline)
            {
                Debug.LogWarning("No internet connection. Starting in offline mode...");

                StartSingleplayer();
                return;
            }

            // If Online, use unity services
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Unity Services startup failed: {e}");
            }
        }

        public async void CreateRelay()
        {
            if (_createdRelay) return;

            _createdRelay = true;

            if (_singleplayer || _isOffline)
            {
                StartSingleplayer();
                return;
            }

            try
            {
                _createdRelay = true;

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(7);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                GUIUtility.systemCopyBuffer = joinCode;
                Debug.Log(allocation.Region);
                Debug.Log("Join Code: " + joinCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

                Loader.IsHost = true;
                Loader.Load(Loader.Scene.GameScene);
            }
            catch (RelayServiceException e)
            {
                _createdRelay = false;
                Debug.LogError(e);
            }
        }

        public async void JoinRelay(string joinCode)
        {
            if (_singleplayer || _joinedRelay || _isOffline) return;

            try
            {
                _joinedRelay = true;

                Debug.Log($"Joining Relay with {joinCode}");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

                Loader.IsHost = false;
                Loader.Load(Loader.Scene.GameScene);
            }
            catch (RelayServiceException e)
            {
                Debug.LogError(e);
            }
        }

        private void StartSingleplayer()
        {
            var manager = NetworkManager.Singleton;
            var transport = manager.GetComponent<SinglePlayerTransport>();
            if (transport == null) transport = manager.gameObject.AddComponent<SinglePlayerTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            Loader.IsHost = true;
            Loader.Load(Loader.Scene.GameScene);
        }
    }
}
