using LandmarkEmulator.Gateway.Network.Message;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LandmarkEmulator.Gateway.Network
{
    /// <summary>
    /// The client build a connection logs in with (Gateway LoginRequest.ClientBuild, e.g. "0.589.1.128895" for the
    /// Steam client, "0.695.…" for the final build).
    /// </summary>
    /// <remarks>
    /// The server speaks the 0.695 client only, its opcodes and sub numbers natively (docs/protocol.md §1.6): the 0.589
    /// client numbers four families differently (Claims 0xF9, Mail 0xF8, InGamePurchase 0x45, InstancedGame 0x10D) and
    /// reads PlayerUpdate 0x26 with a one-byte sub, so it is refused at the gateway login rather than served packets it
    /// would misread. Until 2026-10-17 a header translation (ClientWire695 / ZoneOpcodeTranslator) let one server serve
    /// both; 0.589 is served by the frozen client-0.589 branch.
    /// <para>
    /// The 0.522 client sends LandmarkClientProtocol_694, which the login refused as an unknown protocol.
    /// <see cref="Allowed"/> lists the builds admitted that way. The WebApi's RequiredClientBuild is no part of this: only
    /// the launcher reads it, from /info.
    /// </para>
    /// </remarks>
    public static class ClientBuilds
    {
        /// <summary>The 0.589 (Steam) client, which this server does not serve.</summary>
        public static bool Is589(string clientBuild) => clientBuild?.StartsWith("0.589.") ?? false;

        /// <summary>The 0.522 client: the zone gives it its own login packets (WorldSession.Login522).</summary>
        public static bool Is522(string clientBuild) => clientBuild?.StartsWith("0.522.") ?? false;

        /// <summary>The builds admitted with a protocol the server doesn't speak natively (<see cref="Admit"/>).</summary>
        public static IReadOnlySet<string> Allowed { get; private set; } = new HashSet<string> { "0.522.3.116271" };

        /// <summary>Sets <see cref="Allowed"/> from a comma or space separated list.</summary>
        public static void Allow(string builds)
            => Allowed = (builds ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        /// <summary>The converter for a build (the 0.695 client needs none).</summary>
        public static IClientTranslator TranslatorFor(string clientBuild)
            => Is522(clientBuild) && Allowed.Contains(clientBuild)
                ? new Client522Translator() : PassThroughTranslator.Instance;

        /// <summary>
        /// The login's check of what the client says it is. <c>Served</c> is the protocol the zone handles the connection
        /// with, <c>Refusal</c> the reason it is turned away (null when it is admitted).
        /// </summary>
        public static (ClientProtocol Served, string Refusal) Admit(string clientProtocol, string clientBuild)
        {
            if (!Enum.TryParse(clientProtocol, out ClientProtocol protocol))
                return (default, $"Unsupported Client Protocol: {clientProtocol}.");
            // The server speaks the 0.695 numbering only (docs/protocol.md §1.6).
            if (Is589(clientBuild))
                return (default, $"Client build {clientBuild} is not served here (0.695 only).");
            if (protocol == ClientProtocol.LandmarkClientProtocol_694)
                return Allowed.Contains(clientBuild ?? "")
                    ? (ClientProtocol.LandmarkClientProtocol_709, null)
                    : (default, $"Client build {clientBuild} ({clientProtocol}) is not in ALLOWED_CLIENT_BUILDS.");
            return (protocol, null);
        }
    }
}
