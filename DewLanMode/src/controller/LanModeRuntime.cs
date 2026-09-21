using System;
using System.Net;
using System.Net.Sockets;
using DewLanMode.config;
using Mirror;
using UnityEngine;

namespace DewLanMode;

internal static class LanModeRuntime
{
    public const string HandshakePrefix = "DewLanMode/4/";
    private const int LanMaxMessageSize = 1024 * 1024;

    private static ushort _sessionPort = 7777;
    private static bool _enabled;

    public static bool IsEnabled => DewLanMode.Instance != null && _enabled;

    public static ushort Port => _sessionPort;

    public static bool TryPrepareHost(int port, out string error)
    {
        if (!TrySetPort(port, out error))
        {
            return false;
        }

        _enabled = true;
        return true;
    }

    public static bool TryPrepareJoin(string address, int port, out string error)
    {
        address = address?.Trim();
        if (string.IsNullOrEmpty(address) ||
            (!IPAddress.TryParse(address, out _) && Uri.CheckHostName(address) != UriHostNameType.Dns))
        {
            error = LocalizationSource.GetLocalizationText("LanMenu.InvalidAddress");
            return false;
        }

        if (!TrySetPort(port, out error))
        {
            return false;
        }

        _enabled = true;
        return true;
    }

    public static void Reset()
    {
        LanAuthenticationSceneSync.Reset();
        _enabled = false;
        _sessionPort = 7777;
    }

    public static void PrepareSettings(DewNetworkStartSettings settings)
    {
        if (!IsEnabled || settings == null || !IsMultiplayer(settings.networkMode))
        {
            return;
        }

        settings.lanMode = true;
        if (IsHost(settings.networkMode))
        {
            settings.address = GetPreferredLanAddress();
        }
    }

    public static void ConfigureTransport(DewNetworkManager manager)
    {
        if (!IsEnabled || manager == null || !DewNetworkManager.startSettings.lanMode)
        {
            return;
        }

        TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
        if (transport == null)
        {
            Debug.LogError("[DewLanMode] TelepathyTransport is missing from DewNetworkManager.");
            return;
        }

        transport.port = _sessionPort;
        transport.serverMaxMessageSize = LanMaxMessageSize;
        transport.clientMaxMessageSize = LanMaxMessageSize;
        manager.networkAddress = DewNetworkManager.startSettings.address;
        Debug.Log($"[DewLanMode] LAN transport prepared on port {_sessionPort} with a " +
                  $"{LanMaxMessageSize}-byte message limit.");
    }

    public static string CreateHandshake()
    {
        return HandshakePrefix + _sessionPort;
    }

    public static bool IsCompatibleHandshake(string value)
    {
        return string.Equals(value, CreateHandshake(), StringComparison.Ordinal);
    }

    public static bool IsLanSession()
    {
        return IsEnabled && DewNetworkManager.startSettings?.lanMode == true;
    }

    private static bool IsMultiplayer(DewNetworkMode mode)
    {
        return mode != DewNetworkMode.Singleplayer;
    }

    private static bool IsHost(DewNetworkMode mode)
    {
        return mode == DewNetworkMode.MultiplayerHost || mode == DewNetworkMode.MultiplayerHostRestart;
    }

    public static string GetPreferredLanAddress()
    {
        try
        {
            IPAddress fallback = null;
            foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    fallback ??= address;
                    byte[] bytes = address.GetAddressBytes();
                    bool isPrivate = bytes[0] == 10 ||
                                     (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                                     (bytes[0] == 192 && bytes[1] == 168);
                    if (isPrivate)
                    {
                        return address.ToString();
                    }
                }
            }

            if (fallback != null)
            {
                return fallback.ToString();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DewLanMode] Failed to determine the local IPv4 address: {exception.Message}");
        }

        return "127.0.0.1";
    }

    private static bool TrySetPort(int port, out string error)
    {
        if (port < 1024 || port > 65535)
        {
            error = LocalizationSource.GetLocalizationText("LanMenu.InvalidPort");
            return false;
        }

        _sessionPort = (ushort)port;
        error = null;
        return true;
    }
}
