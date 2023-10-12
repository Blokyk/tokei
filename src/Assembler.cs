using System.Runtime.InteropServices;

namespace Tokei;

public static partial class Assembler
{
    // todo: take a ROS<char> instead of string
    public static ReadOnlySpan<byte> Assemble(string src) {
        var tokens = Scan(src);
        var instrs = Parse(tokens);
        var encodedInstrs = new uint[instrs.Length];

        for (int i = 0; i < instrs.Length; i++)
            encodedInstrs[i] = Encode(instrs[i]);

        return MemoryMarshal.Cast<uint, byte>(encodedInstrs.AsSpan());
    }
}