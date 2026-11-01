using NLog;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// The login port the 0.522 client connects to: every client gets its own socket to the login server, and what passes
    /// between them goes through its <see cref="LoginRewriter522"/>. The server is not changed and doesn't know.
    /// </summary>
    public sealed class LoginRelay522 : IDisposable
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);

        private sealed class Client
        {
            public UdpClient ToServer;
            public LoginRewriter522 Rewriter = new();
            public DateTime LastSeen = DateTime.UtcNow;
        }

        private readonly UdpClient listener;
        private readonly IPEndPoint server;
        private readonly ConcurrentDictionary<IPEndPoint, Client> clients = new();
        private readonly CancellationTokenSource stop = new();

        public LoginRelay522(int port, IPEndPoint loginServer)
        {
            server = loginServer;
            listener = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            IgnoreConnectionReset(listener);
        }

        /// <summary>Without this a Windows socket stops receiving after an ICMP "port unreachable".</summary>
        private static void IgnoreConnectionReset(UdpClient socket)
        {
            if (OperatingSystem.IsWindows())
                socket.Client.IOControl(unchecked((int)0x9800000C), new byte[4], null);
        }

        /// <summary>Runs until <see cref="Dispose"/>.</summary>
        public async Task Run()
        {
            log.Info($"0.522 login relay: {listener.Client.LocalEndPoint} to {server}.");
            while (!stop.IsCancellationRequested)
            {
                UdpReceiveResult received;
                try
                {
                    received = await listener.ReceiveAsync(stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException e)
                {
                    log.Warn($"0.522 login relay: {e.Message}");
                    continue;
                }

                Client client = clients.GetOrAdd(received.RemoteEndPoint, Connect);
                client.LastSeen = DateTime.UtcNow;
                try
                {
                    byte[] data = client.Rewriter.FromClient(received.Buffer);
                    await client.ToServer.SendAsync(data, data.Length);
                }
                catch (Exception e)
                {
                    log.Error(e, "0.522 login relay: client packet failed.");
                }
                Sweep();
            }
        }

        private Client Connect(IPEndPoint endpoint)
        {
            log.Info($"0.522 login relay: {endpoint} connected.");
            var client = new Client { ToServer = new UdpClient() };
            IgnoreConnectionReset(client.ToServer);
            client.ToServer.Connect(server);
            _ = FromServer(endpoint, client);
            return client;
        }

        private async Task FromServer(IPEndPoint endpoint, Client client)
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult received = await client.ToServer.ReceiveAsync(stop.Token);
                    client.LastSeen = DateTime.UtcNow;
                    byte[] data = client.Rewriter.FromServer(received.Buffer);
                    await listener.SendAsync(data, data.Length, endpoint);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    log.Error(e, "0.522 login relay: server packet failed.");
                }
            }
        }

        private void Sweep()
        {
            foreach (var (endpoint, client) in clients)
                if (DateTime.UtcNow - client.LastSeen > Idle && clients.TryRemove(endpoint, out _))
                    client.ToServer.Dispose();
        }

        public void Dispose()
        {
            stop.Cancel();
            listener.Dispose();
            foreach (Client client in clients.Values)
                client.ToServer.Dispose();
        }
    }
}
