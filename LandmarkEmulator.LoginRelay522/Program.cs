using LandmarkEmulator.Gateway.Network.Client522;
using System;
using System.Net;

// Usage: LandmarkEmulator.LoginRelay522 <port> <login server host:port>
if (args.Length != 2 || !int.TryParse(args[0], out int port) || !IPEndPoint.TryParse(args[1], out IPEndPoint server))
{
    Console.WriteLine("Usage: LandmarkEmulator.LoginRelay522 <port> <login server ip:port>");
    return 1;
}

using var relay = new LoginRelay522(port, server);
Console.WriteLine($"0.522 login relay on port {port}, to {server}. Ctrl+C to stop.");
await relay.Run();
return 0;
