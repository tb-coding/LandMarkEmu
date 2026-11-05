using NLog;
using System;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// Abilities 0x27 casts. Layouts of 0.522 from Landmark-Emu (Tool694, AbilityCastSuccess694; 46th to 72nd tests), of
    /// 0.695 from RE-Build AbilityMessages.
    /// </summary>
    public static class CastConverter
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        /// <summary>The 4 opcode and sub bytes and the backtimed header both clients have: sub again, a flag byte, u32 time.</summary>
        private const int Header = 4 + 7;
        public const byte CastSuccessSub = 7, CastFailSub = 6;

        /// <summary>
        /// Sub 1 CastAttempt, client to server. 0.522: {u32 ability, vec3 aim point, …} (126 bytes from a pick); 0.695:
        /// {u64 cast guid, u32 cast hash, u32 counter, u32 ability, vec3 aim point, tail}. 0.522 has no cast guid, hash or
        /// counter: they go as 0 (RE-Build logs the key mismatch and answers as usual). The tail is left empty: RE-Build reads
        /// the tool ring point at tail 44 and falls back to the aim point, which is where 0.522's ring is (Tool694).
        /// </summary>
        public static byte[] AttemptToServer(byte[] packet)
        {
            if (packet.Length < 4 + 16)
            {
                log.Warn($"0.522: dropped a CastAttempt of {packet.Length} bytes, too short for an ability and an aim point.");
                return null;
            }

            var converted = new byte[4 + 16 + 16];
            Array.Copy(packet, converted, 4);
            Array.Copy(packet, 4, converted, 4 + 16, 16);
            return converted;
        }

        /// <summary>
        /// Sub 7 CastSuccess, server to client. 0.695 body: {u64 caster, u64 cast guid, u32 cast hash, u32 ability, u32
        /// animation, u32, u32, bool}; 0.522 (FUN_14379c690): {u64 caster, u64, u32 ability, u32, u32 effect, u32 animation}.
        /// Its handler ends the cast when the ability is the current one, so the held tool can swing again.
        /// </summary>
        public static byte[] SuccessToClient(byte[] packet)
        {
            if (packet.Length < Header + 8 + 8 + 4 + 4 + 4)
            {
                log.Warn($"0.522: dropped a CastSuccess of {packet.Length} bytes, shorter than 0.695's.");
                return null;
            }
            return Write522(packet, BitConverter.ToUInt32(packet, Header + 24));
        }

        /// <summary>
        /// Sub 6 CastFail, server to client: {u64 caster, u64 cast guid, u32 cast hash, u32 ability, u32, u32 animation, u32,
        /// u32 kind}. 0.522's CastFail reader is not looked at yet; a CastSuccess without an animation ends the cast the same
        /// way, so a refused swing doesn't leave the tool waiting.
        /// </summary>
        public static byte[] FailToClient(byte[] packet)
        {
            if (packet.Length < Header + 8 + 8 + 4 + 4)
            {
                log.Warn($"0.522: dropped a CastFail of {packet.Length} bytes, shorter than 0.695's.");
                return null;
            }
            return Write522(packet, 0);
        }

        private static byte[] Write522(byte[] packet, uint animation)
        {
            var converted = new byte[Header + 8 + 8 + 4 + 4 + 4 + 4];
            Array.Copy(packet, converted, Header + 16);                      // header, caster, cast guid
            converted[2] = CastSuccessSub;
            converted[4] = CastSuccessSub;
            Array.Copy(packet, Header + 20, converted, Header + 16, 4);      // ability (after 0.695's cast hash)
            BitConverter.TryWriteBytes(converted.AsSpan(Header + 28), animation);
            return converted;
        }
    }
}
