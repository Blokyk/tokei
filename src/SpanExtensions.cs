static class SpanUtils
{
    public static void Reverse4ByteEndianness(Span<byte> bytes) {
        byte b0, b1, b2, b3;
        for (int i = 3; i < bytes.Length; i += 4) {
            b0 = bytes[i];
            b1 = bytes[i - 1];
            b2 = bytes[i - 2];
            b3 = bytes[i - 3];

            bytes[i] = b3;
            bytes[i-1] = b2;
            bytes[i-2] = b1;
            bytes[i-3] = b0;
        }
    }
}