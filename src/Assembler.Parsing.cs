namespace Tokei;

public static partial class Assembler
{
    private static Instruction[] Parse(Token[] tokenArray) {
        var tokens = new Stack<Token>(tokenArray.Reverse());

        var instrs = new List<Instruction>();
        var jumpFixupInfo = new List<(int jumpIdx, string label)>();
        var labelPositions = new Dictionary<string, int>();

        while (tokens.Count != 0) {
            var token = tokens.Pop();

            if (token == Token.Newline)
                continue;

            if (token is not Token.Identifier ident)
                throw new Exception($"Expected either a label or an instruction, but got '{token}'");

            // if the next token is a ':', then this is a label declaration
            if (tokens.TryPeek(out var nextToken) && nextToken is Token.Delimiter { Char: ':' }) {
                _ = tokens.Pop();
                // Console.WriteLine($"Found label '{ident.Text}' at offset {instrs.Count}");
                labelPositions.Add(ident.Text, instrs.Count); // not -1 because it's the instr AFTER the label we're referencing
            } else {
                var instr = ParseInstruction(ident, ref tokens, out var labelName);
                instrs.Add(instr);

                if (labelName is not null)
                    jumpFixupInfo.Add((instrs.Count-1, labelName));

                if (tokens.TryPop(out token) && token != Token.Newline)
                    throw new Exception("Expected a newline after an instruction");
            }
        }

        foreach (var (jumpIdx, label) in jumpFixupInfo) {
            var jump = (Instruction.JumpLike)instrs[jumpIdx];

            if (!labelPositions.TryGetValue(label, out var labelPos))
                throw new Exception($"Couldn't find label '{label}', used in instruction '{Disassembler.FormatJumpWithLabel(jump, label)}'");

            var offset = labelPos - jumpIdx;
            instrs[jumpIdx] = jump with { Offset = 4*offset };
        }

        return instrs.ToArray();
    }

    static bool TryGetRegisterValue(string str, out byte regIdx) {
        const byte INVALID_REG_IDX = 255;
        regIdx = INVALID_REG_IDX;

        if (str.Length < 2)
            return false;

        byte parseRegIdx()
            => byte.TryParse(str.AsSpan(1), out var num)
                ? num
                : INVALID_REG_IDX;

        byte parseNonSpecialReg() {
            byte rawIdx;
            switch (str[0]) {
                case 'x':
                    rawIdx = parseRegIdx();
                    if (rawIdx <= 31)
                        return parseRegIdx();
                    goto default;
                case 't':
                    // t0-t2 => x5-x7
                    // t3-t6 => x28-x31
                    rawIdx = parseRegIdx();
                    if (rawIdx <= 2)
                        return (byte)(rawIdx + 5);
                    if (rawIdx <= 6)
                        return (byte)(rawIdx + 25);
                    goto default;
                case 's':
                    // s0-s1 => x8-x9
                    // s2-11 => x18-x27
                    rawIdx = parseRegIdx();
                    if (rawIdx <= 1)
                        return (byte)(rawIdx + 8);
                    if (rawIdx <= 11)
                        return (byte)(rawIdx + 16);
                    goto default;
                case 'a':
                    // a0-a7 => x10-x17
                    rawIdx = parseRegIdx();
                    if (rawIdx <= 7)
                        return (byte)(rawIdx + 10);
                    goto default;
                default:
                    return INVALID_REG_IDX;
            }
        }

        regIdx = str switch {
            "zero" => 0,
            "ra" => 1,
            "sp" => 2,
            "gp" => 3,
            "tp" => 4,
            "fp" => 8,
            _ => parseNonSpecialReg()
        };

        return regIdx != 255;
    }

    private static int ParseLabelOrOffset(Token token, int bits, InstrCode code, out string? label) {
        label = null;

        if (token is Token.Number num) {
            var val = num.Value;
            long cutoffBits
                = val < 0
                ? ~(val >> bits)
                : (val >>> bits);

            if (cutoffBits != 0x0)
                throw new Exception($"{code}: expected a ({bits}-bit) number, but '{val}' is too large.");
            return (int)val;
        }

        if (token is Token.Identifier ident) {
            // registers are not allowed
            if (TryGetRegisterValue(ident.Text, out _))
                throw new Exception($"{code}: expected either a label or a numerical offset, but got a register name instead");
            label = ident.Text;
            // labels will be fixed-up later by parser, so it doesn't matter what we put here
            return 0;
        }

        throw new Exception($"{code}: expected either a label or a numerical offset, but got '{token}' instead");
    }

    private static Token[] ParseInstructionOperands(ref Stack<Token> tokens) {
        var operandTokens = new List<Token>(3);
        while (tokens.TryPop(out var nextToken)) {
            if (nextToken == Token.Newline)
                break;

            switch (nextToken) {
                case Token.Identifier ident:
                    operandTokens.Add(ident);
                    break;
                case Token.Number num:
                    // if this is just a normal number
                    if (!tokens.TryPeek(out nextToken) || nextToken is not Token.Delimiter { Char: '('}) {
                        operandTokens.Add(num);
                        break;
                    }

                    _ = tokens.Pop();

                    if (!tokens.TryPop(out nextToken) || nextToken is not Token.Identifier registerName)
                        throw new Exception("Unexpected token in offset-and-base expression, expected a register name");

                    if (!tokens.TryPop(out nextToken) || nextToken is not Token.Delimiter { Char: ')'})
                        throw new Exception("Missing ')' in base-and-offset expression");

                    operandTokens.Add(new Token.OffsetAndBase(num, registerName));
                    break;
                default:
                    // push back the token for handling by the "missing comma" thing
                    tokens.Push(nextToken);
                    break;
            }

            // if we're at the end of the file, we won't be able to pop anymore,
            // so no need to check for a comma
            if (!tokens.TryPop(out nextToken))
                break;

            if (nextToken is Token.Delimiter delim) {
                if (delim.Char is '\n')
                    break;
                if (delim.Char is ',')
                    continue;
            }

            throw new Exception("Missing ',' between instruction operands");
        }
        // we have to push back the newline we last consumed to leave the stream intact
        tokens.Push(Token.Newline);
        return operandTokens.ToArray();
    }
}