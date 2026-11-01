using System;
using System.Collections.Generic;

namespace LandmarkEmulator.Gateway.Network.Client522
{
    /// <summary>
    /// LZ4 block format (no frame header), used by each record of CommandBase ItemDefinitions (sub opcode 0x4B).
    /// </summary>
    /// <remarks>
    /// The repo has no LZ4 package, the item-support branch used LZ4.LZ4Codec. Decoding follows the LZ4 block specification.
    /// <see cref="Encode"/> writes a single literal only sequence: valid LZ4 that every decoder accepts, only a little larger than the input.
    /// </remarks>
    public static class Lz4Block
    {
        /// <summary>
        /// Decodes an LZ4 block, the decoded size has to be exactly <paramref name="decodedLength"/>.
        /// </summary>
        public static byte[] Decode(ReadOnlySpan<byte> input, int decodedLength)
        {
            byte[] output = new byte[decodedLength];
            int inputPosition = 0;
            int outputPosition = 0;

            while (inputPosition < input.Length)
            {
                byte token = input[inputPosition++];

                int literalLength = token >> 4;
                if (literalLength == 15)
                    literalLength += ReadLength(input, ref inputPosition);

                if (inputPosition + literalLength > input.Length || outputPosition + literalLength > output.Length)
                    throw new InvalidOperationException("LZ4 literals run past the end of the block.");
                input.Slice(inputPosition, literalLength).CopyTo(output.AsSpan(outputPosition));
                inputPosition  += literalLength;
                outputPosition += literalLength;

                // The last sequence has only literals.
                if (inputPosition >= input.Length)
                    break;

                if (inputPosition + 2 > input.Length)
                    throw new InvalidOperationException("LZ4 match offset is truncated.");
                int offset = input[inputPosition] | (input[inputPosition + 1] << 8);
                inputPosition += 2;
                if (offset == 0 || offset > outputPosition)
                    throw new InvalidOperationException($"LZ4 match offset {offset} is invalid at output position {outputPosition}.");

                int matchLength = token & 0x0F;
                if (matchLength == 15)
                    matchLength += ReadLength(input, ref inputPosition);
                matchLength += 4;

                if (outputPosition + matchLength > output.Length)
                    throw new InvalidOperationException("LZ4 match runs past the decoded length.");
                // Byte by byte, the match may overlap the bytes it writes.
                for (int i = 0; i < matchLength; i++, outputPosition++)
                    output[outputPosition] = output[outputPosition - offset];
            }

            if (outputPosition != output.Length)
                throw new InvalidOperationException($"LZ4 block decoded to {outputPosition} bytes, expected {output.Length}.");
            return output;
        }

        private static int ReadLength(ReadOnlySpan<byte> input, ref int position)
        {
            int length = 0;
            byte value;
            do
            {
                if (position >= input.Length)
                    throw new InvalidOperationException("LZ4 length is truncated.");
                value = input[position++];
                length += value;
            }
            while (value == 255);
            return length;
        }

        /// <summary>
        /// Encodes the data as one LZ4 sequence of literals (no matches).
        /// </summary>
        public static byte[] Encode(ReadOnlySpan<byte> data)
        {
            var output = new List<byte>(data.Length + data.Length / 255 + 2);
            int literalLength = data.Length;
            if (literalLength < 15)
                output.Add((byte)(literalLength << 4));
            else
            {
                output.Add(0xF0);
                int rest = literalLength - 15;
                while (rest >= 255)
                {
                    output.Add(255);
                    rest -= 255;
                }
                output.Add((byte)rest);
            }

            output.AddRange(data.ToArray());
            return output.ToArray();
        }
    }
}
