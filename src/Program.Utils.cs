namespace Tokei;

internal static partial class TokeiApp
{
    internal readonly struct MultiBaseInt {
        public readonly int Value;
        public MultiBaseInt(int val) {
            Value = val;
        }

        public static MultiBaseInt Parse(string s) {
            var v = Utils.ParseDecimalOrHexOrBin(s);

            if (v is < Int32.MinValue or > Int32.MaxValue)
                throw new Exception($"Value {v} is too big to fit in an 32-bit int!");

            return (int)v;
        }

        public static bool IsPositive(MultiBaseInt mbi) => Int32.IsPositive(mbi);
        public static bool IsNegative(MultiBaseInt mbi) => Int32.IsNegative(mbi);

        public static implicit operator int(MultiBaseInt mbi) => mbi.Value;
        public static implicit operator MultiBaseInt(int i) => new(i);
    }

    public static void CheckFileIsWritable(FileInfo file) {
        if (file.Exists && file.IsReadOnly)
            throw new IOException($"File '{file}' cannot be written to");
    }

    public static byte[] ParseRegisterList(string s) {
        var parts = s.Split(',');

        var regs = new byte[parts.Length];

        for (int i = 0; i < parts.Length; i++) {
            string part = parts[i];

            if (part.Length < 2 || part[0] != 'x' || !Byte.TryParse(part.AsSpan(1), out var num))
                throw new Exception("Register names must be in the format 'x2,x5,x9,...,xN'");
            if (num is < 0 or > 31)
                throw new Exception("Register index must be between 0 and 31 (both included)");
            regs[i] = num;
        }

        return regs;
    }

    public static Range ParseRange(string s) {
        var parts = s.Split(':', 2);

        if (!Int32.TryParse(parts[0], out var start))
            throw new Exception($"Couldn't parse '{parts[0]}' as an integer");
        if (!Int32.TryParse(parts[1], out var end))
            throw new Exception($"Couldn't parse '{parts[1]}' as an integer");

        return new(
            start >= 0 ? Index.FromStart(start) : Index.FromEnd(start),
            end >= 0 ? Index.FromStart(end) : Index.FromEnd(end)
        );
    }

    public static ReadOnlySpan<byte> ReadBinFile(FileInfo file, Endianness endianness, int offset) {
        var fileStream = file.OpenRead();
        var fileLength = fileStream.Length;

        if (fileLength > Int32.MaxValue)
            throw new Exception("Can't read instructions from a file bigger than 2GiB (" + fileLength + ")");

        using var binStream = new BinaryReader(fileStream);
        var bytes = binStream.ReadBytes((int)fileLength).AsSpan(offset);

        if (endianness is Endianness.Big)
            SpanUtils.Reverse4ByteEndianness(bytes);

        return bytes;
    }
}