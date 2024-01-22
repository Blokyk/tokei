namespace Tokei;

public static partial class Assembler
{
    abstract record Token() {
        public sealed record Identifier(string Text) : Token;
        public sealed record Number(long Value) : Token;
        internal sealed record OffsetAndBase(Number Offset, Identifier BaseRegister) : Token;

        public static readonly Delimiter Newline = new('\n');
        public sealed record Delimiter(char Char) : Token;
    }

    private static Token[] Scan(string src) {
        var tokens = new List<Token>();

        for (int i = 0; i < src.Length; i++) {
            if (src[i] is ' ' or '\t' or '\r')
                continue;

            if (src[i] is '\n') {
                tokens.Add(Token.Newline);
                continue;
            }

            if (src[i] is '#') {
                do { i++; }
                while (i < src.Length && src[i] != '\n');

                tokens.Add(Token.Newline);

                continue;
            }

            if (Char.IsAsciiLetter(src[i]) || src[i] is '_') {
                var startIdx = i;

                do { i++; }
                while (i < src.Length && (Char.IsAsciiLetterOrDigit(src[i]) || src[i] is '.' or '_'));

                tokens.Add(new Token.Identifier(src[startIdx..i]));
                i--;
                continue;
            }

            if (Char.IsAsciiDigit(src[i]) || src[i] is '+' or '-') {
                var startIdx = i;

                bool shouldNegate = src[i] is '-';
                if (shouldNegate || src[i] is '+') {
                    startIdx = ++i; // ignore the sign character
                }

                do { i++; }
                while (i < src.Length && (Char.IsAsciiHexDigit(src[i]) || src[i] is 'x' or 'b'));

                long value = Utils.ParseDecimalOrHexOrBin(src.AsSpan()[startIdx..i]);

                if (shouldNegate)
                    value = -value;

                tokens.Add(new Token.Number(value));
                i--;
                continue;
            }

            tokens.Add(new Token.Delimiter(src[i]));
        }

        return tokens.ToArray();
    }
}