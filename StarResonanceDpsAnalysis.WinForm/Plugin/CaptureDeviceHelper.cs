using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using System.Runtime.InteropServices;

namespace StarResonanceDpsAnalysis.WinForm.Plugin
{
    public static class CaptureDeviceHelper
    {
        /// <summary>
        /// Tries to determine the best network card index by scanning the routing table using GetBestInterface.
        /// Falls back to the previous methods if route table lookup fails.
        /// </summary>
        public static int GetBestNetworkCardIndex(CaptureDeviceList devices)
        {
            try
            {
                // First try route table based approach
                var routeIndex = GetBestInterfaceForExternalDestination();
                if (routeIndex != null)
                {
                    var ni = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(n =>
                        {
                            try
                            {
                                var props = n.GetIPProperties();
                                var ipv4 = props.GetIPv4Properties();
                                return ipv4 != null && ipv4.Index == routeIndex.Value;
                            }
                            catch
                            {
                                return false;
                            }
                        });

                    if (ni != null)
                    {
                        int bestIndex = -1, bestScore = -1;
                        for (var i = 0; i < devices.Count; i++)
                        {
                            var score = 0;
                            if (devices[i].Description.Contains(ni.Name, StringComparison.OrdinalIgnoreCase)) score += 2;
                            if (devices[i].Description.Contains(ni.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
                            if (score <= bestScore) continue;
                            bestScore = score;
                            bestIndex = i;
                        }

                        if (bestIndex >= 0) return bestIndex;
                    }
                }

                // If route table approach failed, fall back to outbound local IP method
                var localIp = GetOutboundLocalIp();
                if (localIp == null) return GetBestNetworkCardIndexFallback(devices);

                // Find the network interface which has this local IP
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var activeNi = interfaces
                    .FirstOrDefault(ni => ni.GetIPProperties()
                        .UnicastAddresses.Any(ua => ua.Address is { AddressFamily: AddressFamily.InterNetwork } &&
                                                     ua.Address.Equals(localIp)));

                // Fallback: try to find any interface that has an address in the same subnet
                activeNi ??= interfaces
                    .FirstOrDefault(ni => ni.GetIPProperties().UnicastAddresses
                        .Any(ua => ua.Address is { AddressFamily: AddressFamily.InterNetwork } &&
                                   AreInSameSubnet(ua.Address, localIp, ua.IPv4Mask)));

                if (activeNi == null)
                {
                    // last fallback: Use old method
                    return GetBestNetworkCardIndexFallback(devices);
                }

                // Match highest scoring SharpPcap device
                int best = -1, bestSc = -1;
                for (var i = 0; i < devices.Count; i++)
                {
                    var score = 0;
                    if (devices[i].Description.Contains(activeNi.Name, StringComparison.OrdinalIgnoreCase)) score += 2;
                    if (devices[i].Description.Contains(activeNi.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
                    if (score <= bestSc) continue;
                    bestSc = score;
                    best = i;
                }
                return best;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DetermineInterfaceByRouteTable: failed to determine outbound interface: {ex.Message}");

                return devices.Count > 0 ? 0 : -1;
            }
        }

        private static bool AreInSameSubnet(IPAddress? addr1, IPAddress? addr2, IPAddress? mask)
        {
            if (addr1 == null || addr2 == null || mask == null) return false;
            var a1 = addr1.GetAddressBytes();
            var a2 = addr2.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a1.Length != a2.Length || a1.Length != m.Length) return false;
            return !a1.Where((t, i) => (t & m[i]) != (a2[i] & m[i])).Any();
        }

        public static IPAddress? GetOutboundLocalIp()
        {
            try
            {
                // connect to a well-known external endpoint (no packets are sent for UDP on Connect)
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("114.114.114.114", 65530);
                if (socket.LocalEndPoint is IPEndPoint ep)
                {
                    return ep.Address;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetOutboundLocalIp failed: {ex.Message}");
            }

            return null;
        }

        private static int GetBestNetworkCardIndexFallback(CaptureDeviceList devices)
        {
            var active = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.GetIPProperties().UnicastAddresses.Any(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork))
                .OrderByDescending(ni => ni.GetIPProperties().GatewayAddresses.Count != 0) // gateway preferred
                .FirstOrDefault();

            if (active == null) return devices.Count > 0 ? 0 : -1;

            // Match highest scoring device
            int bestIndex = -1, bestScore = -1;
            for (int i = 0; i < devices.Count; i++)
            {
                int score = 0;
                if (devices[i].Description.Contains(active.Name, StringComparison.OrdinalIgnoreCase)) score += 2;
                if (devices[i].Description.Contains(active.Description, StringComparison.OrdinalIgnoreCase)) score += 3;
                if (score > bestScore) { bestScore = score; bestIndex = i; }
            }
            return bestIndex;
        }

        // PInvoke to call GetBestInterface from iphlpapi.dll
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);

        private static int? GetBestInterfaceForExternalDestination()
        {
            try
            {
                var dest = IPAddress.Parse("8.8.8.8");
                // Convert IP address to uint in network byte order as required by Windows GetBestInterface API
                var bytes = dest.GetAddressBytes();
                var addr = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? bytes.Reverse().ToArray() : bytes, 0);

                if (GetBestInterface(addr, out var index) == 0)
                {
                    return (int)index;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetBestInterfaceForExternalDestination failed: {ex.Message}");
            }

            return null;
        }
    }
}
