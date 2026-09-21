using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DewLanMode;

internal sealed class LanDiscoveredLobby
{
    public string Address;
    public int Port;
    public string Name;
    public int CurrentPlayers;
    public int MaxPlayers;
    public bool HasGameStarted;

    public LobbyInstance ToLobbyInstance()
    {
        return new LobbyInstance
        {
            id = Address + ":" + Port,
            version = Application.version,
            isModded = true,
            lobbyName = Name,
            lobbyDescription = Address + ":" + Port,
            difficulty = "diffNormal",
            maxPlayers = MaxPlayers,
            currentPlayers = CurrentPlayers,
            hasGameStarted = HasGameStarted,
            allowJoin = CurrentPlayers < MaxPlayers,
            allowMidJoins = AllowMidJoinType.AllowAll,
            connectionQuality = LobbyConnectionQuality.Best,
            gameServerAddress = Address
        };
    }
}

internal sealed class LanDiscoveryService : MonoBehaviour
{
    private const int DiscoveryPort = 47777;
    private const string RequestMagic = "DewLanMode.Discover/1";
    private const string ResponseMagic = "DewLanMode.Lobby/1";
    private const float SearchDuration = 1.25f;

    private sealed class DiscoveryResponse
    {
        public string Magic;
        public int Port;
        public string Name;
        public int CurrentPlayers;
        public int MaxPlayers;
        public bool HasGameStarted;
    }

    private UdpClient _advertiser;
    private UdpClient _searchClient;
    private Action<IReadOnlyList<LanDiscoveredLobby>> _searchCompleted;
    private readonly Dictionary<string, LanDiscoveredLobby> _searchResults =
        new Dictionary<string, LanDiscoveredLobby>(StringComparer.OrdinalIgnoreCase);
    private float _searchDeadline;

    public static LanDiscoveryService Instance { get; private set; }

    public static LanDiscoveryService Install(GameObject owner)
    {
        LanDiscoveryService service = owner.GetComponent<LanDiscoveryService>();
        if (service == null)
        {
            service = owner.AddComponent<LanDiscoveryService>();
        }

        Instance = service;
        return service;
    }

    public void StartAdvertising()
    {
        StopAdvertising();
        if (!LanModeRuntime.IsLanSession() || !NetworkServer.active)
        {
            return;
        }

        try
        {
            _advertiser = CreateUdpClient();
            _advertiser.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            Debug.Log($"[DewLanMode] Advertising LAN lobby on UDP port {DiscoveryPort}.");
        }
        catch (Exception exception)
        {
            StopAdvertising();
            Debug.LogWarning($"[DewLanMode] Could not advertise the LAN lobby: {exception.Message}");
        }
    }

    public void StopAdvertising()
    {
        _advertiser?.Close();
        _advertiser = null;
    }

    public void Search(Action<IReadOnlyList<LanDiscoveredLobby>> completed)
    {
        CancelSearch();
        _searchCompleted = completed;
        _searchResults.Clear();
        _searchDeadline = Time.unscaledTime + SearchDuration;

        try
        {
            _searchClient = CreateUdpClient();
            _searchClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            byte[] request = Encoding.UTF8.GetBytes(RequestMagic);
            _searchClient.Send(request, request.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewLanMode] LAN discovery failed to start: {exception.Message}");
            CompleteSearch();
        }
    }

    public void CancelSearch()
    {
        _searchClient?.Close();
        _searchClient = null;
        _searchCompleted = null;
        _searchResults.Clear();
    }

    private void Update()
    {
        PollAdvertiser();
        PollSearchClient();

        if (_searchClient != null && Time.unscaledTime >= _searchDeadline)
        {
            CompleteSearch();
        }
    }

    private void PollAdvertiser()
    {
        if (_advertiser == null)
        {
            return;
        }

        try
        {
            while (_advertiser.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = _advertiser.Receive(ref remote);
                if (!string.Equals(Encoding.UTF8.GetString(bytes), RequestMagic, StringComparison.Ordinal))
                {
                    continue;
                }

                DiscoveryResponse response = CreateResponse();
                byte[] payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
                _advertiser.Send(payload, payload.Length, remote);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException exception)
        {
            Debug.LogWarning($"[DewLanMode] LAN advertiser socket error: {exception.Message}");
        }
    }

    private void PollSearchClient()
    {
        if (_searchClient == null)
        {
            return;
        }

        try
        {
            while (_searchClient.Available > 0)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = _searchClient.Receive(ref remote);
                if (bytes.Length > 4096)
                {
                    continue;
                }

                DiscoveryResponse response = JsonConvert.DeserializeObject<DiscoveryResponse>(Encoding.UTF8.GetString(bytes));
                if (response == null || response.Magic != ResponseMagic || response.Port < 1024 ||
                    response.Port > 65535 || response.MaxPlayers < 1 || response.MaxPlayers > 64)
                {
                    continue;
                }

                string address = remote.Address.ToString();
                string key = address + ":" + response.Port;
                _searchResults[key] = new LanDiscoveredLobby
                {
                    Address = address,
                    Port = response.Port,
                    Name = LimitName(string.IsNullOrWhiteSpace(response.Name) ? address : response.Name.Trim()),
                    CurrentPlayers = Mathf.Clamp(response.CurrentPlayers, 0, response.MaxPlayers),
                    MaxPlayers = response.MaxPlayers,
                    HasGameStarted = response.HasGameStarted
                };
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewLanMode] Ignoring invalid LAN discovery response: {exception.Message}");
        }
    }

    private DiscoveryResponse CreateResponse()
    {
        DewNetworkStartSettings settings = DewNetworkManager.startSettings;
        int maxPlayers = Mathf.Clamp(settings?.maxPlayers ?? 4, 1, 64);
        int currentPlayers = NetworkServer.active ? NetworkServer.connections.Count : 1;
        string lobbyName = settings?.lobbyName;
        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            lobbyName = DewSave.profileMain?.name + " - LAN";
        }

        return new DiscoveryResponse
        {
            Magic = ResponseMagic,
            Port = LanModeRuntime.Port,
            Name = lobbyName,
            CurrentPlayers = Mathf.Max(1, currentPlayers),
            MaxPlayers = maxPlayers,
            HasGameStarted = SceneManager.GetActiveScene().name == "PlayGame"
        };
    }

    private static string LimitName(string value)
    {
        return value.Length <= 80 ? value : value.Substring(0, 80);
    }

    private void CompleteSearch()
    {
        Action<IReadOnlyList<LanDiscoveredLobby>> completed = _searchCompleted;
        List<LanDiscoveredLobby> result = new List<LanDiscoveredLobby>(_searchResults.Values);
        result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
        CancelSearch();
        completed?.Invoke(result);
    }

    private static UdpClient CreateUdpClient()
    {
        UdpClient client = new UdpClient(AddressFamily.InterNetwork);
        client.EnableBroadcast = true;
        client.ExclusiveAddressUse = false;
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Blocking = false;
        return client;
    }

    private void OnDestroy()
    {
        StopAdvertising();
        CancelSearch();
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
