using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using QFSW.QC;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Random = UnityEngine.Random;

public class TestNetwork : NetworkBehaviour
{
    private const string KEY_RELAY_JOIN_CODE = "joinCode";
    private const string GAMEMODE = "gameMode";
    private const string PLAYER_NAME = "playerName";
    private const string PLAYER_ID = "playerId";
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;
    private string playerClientId;
    
    private async void Awake()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions initializationOptions = new InitializationOptions();
            initializationOptions.SetProfile(Random.Range(0, 10000).ToString());
            
            await UnityServices.InitializeAsync(initializationOptions);
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log("signin " + AuthenticationService.Instance.PlayerId + " " + AuthenticationService.Instance.PlayerName);
                
                NetworkManager.Singleton.OnServerStarted += () =>
                {
                    Debug.Log("server started!!");
                    playerClientId = NetworkManager.LocalClientId.ToString();
                    Debug.Log(playerClientId);
                    UpdatePlayer("ez");
                };
            
                NetworkManager.Singleton.OnClientConnectedCallback += _ =>
                {
                    if (IsLobbyHost())
                    {
                        return;
                    }
                    Debug.Log("client connected!!");
                    playerClientId = NetworkManager.LocalClientId.ToString();
                    Debug.Log(playerClientId);
                    UpdatePlayer("ez");
                };
                
                NetworkManager.Singleton.OnClientDisconnectCallback += _ =>
                {
                    if (IsLobbyHost())
                    {
                        return;
                    }
                    LeaveLobby();
                };
            };
            
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPollForUpdates();
    }
    
    private async void HandleLobbyHeartbeat()
    {
        if (IsLobbyHost()) {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f) {
                const float heartbeatTimerMax = 15f;
                heartbeatTimer = heartbeatTimerMax;

                await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }
    
    private bool IsLobbyHost() {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }
    
    private async void HandleLobbyPollForUpdates()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer < 0f)
            {
                const float lobbyUpdateTimerMax = 1.1f;
                lobbyUpdateTimer = lobbyUpdateTimerMax;

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
            }
        }
    }

    private async Task<Allocation> AllocateRelay() {
        try {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            return allocation;
        } catch (RelayServiceException e) {
            Debug.Log(e);

            return default;
        }
    }
    
    private async Task<string> GetRelayJoinCode(Allocation allocation) {
        try {
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            return relayJoinCode;
        } catch (RelayServiceException e) {
            Debug.Log(e);
            return default;
        }
    }
    
    private async Task<JoinAllocation> joinRelay(string joinCode)
    {
        try
        {
            Debug.Log("join " + joinCode);
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            return joinAllocation;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            return default;
        }
    }
    
    [Command]
    public async void CreateLobby(string lobbyName, bool isPrivate)
    {
        try
        {
            const int maxPlayers = 4;
            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, new CreateLobbyOptions
            {
                IsPrivate = isPrivate
            });
            Allocation allocation = await AllocateRelay();
            var relayJoinCode = await GetRelayJoinCode(allocation);
            
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions {
                Data = new Dictionary<string, DataObject> {
                    { KEY_RELAY_JOIN_CODE , new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { GAMEMODE, new DataObject(DataObject.VisibilityOptions.Public, "Cap") }
                }
            });
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation,"dtls"));
            
            NetworkManager.Singleton.StartHost();
            
            Debug.Log("create "+ joinedLobby.Name + " " + joinedLobby.MaxPlayers + " " + joinedLobby.Id + " " + joinedLobby.LobbyCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
    
    [Command]
    public async void QuickJoinLobby()
    {
        try
        {
            joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();

            var relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

            JoinAllocation joinAllocation = await joinRelay(relayJoinCode);
            
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation,"dtls"));
            
            NetworkManager.Singleton.StartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
    
    [Command] 
    public async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                },
                Order = new List<QueryOrder>
                {
                    new(false, QueryOrder.FieldOptions.Created)
                }
            };
            
            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log("lobby" + " " + queryResponse.Results.Count);
            foreach (var lobby in queryResponse.Results)
            {
                Debug.Log(lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    [Command]
    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            var relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

            JoinAllocation joinAllocation = await joinRelay(relayJoinCode);
            
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
            
            NetworkManager.Singleton.StartClient();
            
            Debug.Log("join w code " + lobbyCode);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    [Command]
    public async void DeleteLobby() {
        if (joinedLobby != null) {
            try {
                await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);

                joinedLobby = null;
            } catch (LobbyServiceException e) {
                Debug.Log(e);
            }
        }
    }

    [Command]
    public async void LeaveLobby() {
        if (joinedLobby != null) {
            try {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                NetworkManager.Singleton.Shutdown();
                joinedLobby = null;
            } catch (LobbyServiceException e) {
                Debug.Log(e);
            }
        }
    }

    [Command]
    public async void KickPlayer(string playerId)
    {
        if (IsLobbyHost()) {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
                NetworkManager.DisconnectClient(Convert.ToUInt64(GetPlayer().Data[PLAYER_ID].Value));
            } catch (LobbyServiceException e) {
                Debug.Log(e);
            }
        }
    }
    
    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                {PLAYER_ID, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerClientId)}
            }
        };
    }
    
    [Command]
    private void PrintPlayersCMD()
    {
        PrintPlayers(joinedLobby);
    }
    
    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("in lobby " + lobby.Name + " " + lobby.Data[GAMEMODE].Value);
        foreach (var player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data[PLAYER_NAME].Value + " " + player.Data[PLAYER_ID].Value);
        }
    }
    
    [Command]
    private async void UpdateLobbyGameMode(string gameMode)
    {

        try
        {
            joinedLobby = await Lobbies.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { GAMEMODE, new DataObject(DataObject.VisibilityOptions.Public, gameMode) }
                }
            });
            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
    
    [Command]
    private void UpdatePlayer(string newPlayerName)
    {
        playerName = newPlayerName;
        LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                    {PLAYER_ID, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerClientId)}
                }
            });
    }
}