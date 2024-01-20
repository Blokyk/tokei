namespace Tokei;

#pragma warning disable IDE0019

public static partial class Assembler
{
    private abstract record SyntheticInstruction(InstrCode Code) : Instruction(Code) {
        public record InstrWithLabel(Instruction Instruction, string Label) : SyntheticInstruction(Instruction.Code);
        public static readonly Instruction Filler = new Error(InstrCode.ERROR, 0);

        // beqz, bnez
        public record BranchZero(InstrCode Code, byte Rs, int Offset) : SyntheticInstruction(Code);
        // j
        public record JumpAbs(int Offset) : SyntheticInstruction(InstrCode.j);
        // jr
        public record JumpReg(byte Rs) : SyntheticInstruction(InstrCode.jr);
        // la
        public record LoadAddress(byte Rd, int Address) : SyntheticInstruction(InstrCode.la);
        // li
        public record LoadImm(byte Rd, int Imm) : SyntheticInstruction(InstrCode.li);
        // mv, neg, not
        public record RegToReg(InstrCode Code, byte Rd, byte Rs) : SyntheticInstruction(Code);
        // not
        public record Nop() : SyntheticInstruction(InstrCode.nop);
        // ret
        public record Ret() : SyntheticInstruction(InstrCode.ret);
        // seqz, snez
        public record Set(InstrCode Code, byte Rd, byte Rs) : SyntheticInstruction(Code);
    }

    private static Instruction ParseInstruction(Token.Identifier instrName, ref Stack<Token> tokens) {
        static SyntheticInstruction.InstrWithLabel WithLabel(Instruction i, string label) => new(i, label);

        var operands = ParseInstructionOperands(ref tokens);

        if (!InstrCodeUtils.TryParse(instrName.Text, out var code))
            throw new Exception("Unknown instruction: " + instrName.Text);

        switch (code) {
            case InstrCode.add:
            case InstrCode.and:
            case InstrCode.or:
            case InstrCode.sll:
            case InstrCode.slt:
            case InstrCode.sltu:
            case InstrCode.sra:
            case InstrCode.srl:
            case InstrCode.sub:
            case InstrCode.xor: {
                assertOperandCount(3);
                var rdToken = operands[0] as Token.Identifier;
                var rs1Token = operands[1] as Token.Identifier;
                var rs2Token = operands[2] as Token.Identifier;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");
                if (rs1Token is null || !TryGetRegisterValue(rs1Token.Text, out var rs1))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operands[1]}' instead");
                if (rs2Token is null || !TryGetRegisterValue(rs2Token.Text, out var rs2))
                    throw new Exception($"{code}: expected a register name for rs2, but got '{operands[2]}' instead");

                return new Instruction.Register(code, rd, rs1, rs2);
            }

            case InstrCode.addi:
            case InstrCode.andi:
            case InstrCode.ori:
            case InstrCode.slli:
            case InstrCode.slti:
            case InstrCode.sltiu:
            case InstrCode.srai:
            case InstrCode.srli:
            case InstrCode.xori: {
                assertOperandCount(3);
                var rdToken = operands[0] as Token.Identifier;
                var rsToken = operands[1] as Token.Identifier;
                var opToken = operands[2] as Token.Number;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operands[1]}' instead");
                if (opToken is null || !fitsInNBits(opToken.Value, 12))
                    throw new Exception($"{code}: expected a (12-bit) numerical value for the immediate operand, but got '{operands[2]}' instead");

                return new Instruction.Immediate(code, rd, rs, (int)opToken.Value);
            }

            case InstrCode.lb:
            case InstrCode.lbu:
            case InstrCode.ld:
            case InstrCode.lh:
            case InstrCode.lhu:
            case InstrCode.lw:
            case InstrCode.lwu:
            case InstrCode.jalr: {
                // if this is this doesn't have a 'base-and-offset' operand,
                // treat it as a normal immediate instruction
                if (operands.Length != 2 || operands[1] is not Token.OffsetAndBase offsetBaseToken)
                    goto case InstrCode.addi;
                var rdToken = operands[0] as Token.Identifier;
                var rsToken = offsetBaseToken.BaseRegister;
                var offset = offsetBaseToken.Offset.Value;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");
                if (!TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for the base, but got '{rsToken}' instead");
                if (!fitsInNBits(offset, 12))
                    throw new Exception($"{code}: Offset '{offset}' is too large; consider loading a higher value into {rsToken}");

                return new Instruction.Immediate(code, rd, rs, (int)offset);
            }

            case InstrCode.auipc:
            case InstrCode.lui: {
                assertOperandCount(1);
                var rdToken = operands[0] as Token.Identifier;
                var opToken = operands[1] as Token.Number;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");
                if (opToken is null || !fitsInNBits(opToken.Value, 20))
                    throw new Exception($"{code}: expected a (20-bit) numerical value for the immediate operand, but got '{operands[2]}' instead");

                return new Instruction.UpperImmediate(code, rd, (int)opToken.Value);
            }

            case InstrCode.beq:
            case InstrCode.bge:
            case InstrCode.bgeu:
            case InstrCode.blt:
            case InstrCode.bltu:
            case InstrCode.bne: {
                assertOperandCount(3);
                var rs1Token = operands[0] as Token.Identifier;
                var rs2Token = operands[1] as Token.Identifier;

                if (rs1Token is null || !TryGetRegisterValue(rs1Token.Text, out var rs1))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operands[0]}' instead");
                if (rs2Token is null || !TryGetRegisterValue(rs2Token.Text, out var rs2))
                    throw new Exception($"{code}: expected a register name for rs2, but got '{operands[1]}' instead");

                var offset = ParseLabelOrOffset(operands[2], 13, code, out var label);

                var instr = new Instruction.Branch(code, rs1, rs2, offset);
                return label is null ? instr : WithLabel(instr, label);
            }

            case InstrCode.jal: {
                assertOperandCount(2);
                var rdToken = operands[0] as Token.Identifier;
                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");

                var offset = ParseLabelOrOffset(operands[1], 20, code, out var label);

                var instr = new Instruction.Jump(code, rd, offset);
                return label is null ? instr : WithLabel(instr, label);
            }

            case InstrCode.sb:
            case InstrCode.sd:
            case InstrCode.sh:
            case InstrCode.sw: {
                Token.Identifier? rbaseToken;
                Token.Identifier? rsToken;
                Token opRealToken;
                Token.Number? opToken;

                if (operands.Length == 2 && operands[1] is Token.OffsetAndBase offsetBaseToken) {
                    rbaseToken = offsetBaseToken.BaseRegister;
                    opRealToken = opToken = offsetBaseToken.Offset;
                } else {
                    assertOperandCount(3);
                    rbaseToken = operands[0] as Token.Identifier;
                    opToken = (opRealToken = operands[2]) as Token.Number;
                }

                rsToken = operands[0] as Token.Identifier;

                if (rbaseToken is null || !TryGetRegisterValue(rbaseToken.Text, out var rbase))
                    throw new Exception($"{code}: expected a register name for rbase, but got '{operands[0]}' instead");
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operands[1]}' instead");
                if (opToken is null || !fitsInNBits(opToken.Value, 12))
                    throw new Exception($"{code}: expected a (12-bit) numerical value for the immediate operand, but got '{opRealToken}' instead");

                return new Instruction.Store(code, rbase, rs, (int)opToken.Value);
            }

            case InstrCode.beqz:
            case InstrCode.bnez: {
                assertOperandCount(2);
                var rsToken = operands[0] as Token.Identifier;
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operands[0]}' instead");

                var offset = ParseLabelOrOffset(operands[1], 13, code, out var label);

                var instr = new SyntheticInstruction.BranchZero(code, rs, offset);
                return label is null ? instr : WithLabel(instr, label);
            }

            case InstrCode.j: {
                assertOperandCount(1);
                var offset = ParseLabelOrOffset(operands[0], 20, code, out var label);

                var instr = new SyntheticInstruction.JumpAbs(offset);
                return label is null ? instr : WithLabel(instr, label);
            }

            case InstrCode.jr: {
                assertOperandCount(1);
                var rsToken = operands[0] as Token.Identifier;
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operands[0]}' instead");

                return new SyntheticInstruction.JumpReg(rs);
            }

            case InstrCode.la: {
                assertOperandCount(2);
                var rdToken = operands[0] as Token.Identifier;
                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");

                var offset = ParseLabelOrOffset(operands[1], 32, code, out var label);
                var instr = new SyntheticInstruction.LoadAddress(rd, offset);
                return label is null ? instr : WithLabel(instr, label);
            }

            case InstrCode.li: {
                assertOperandCount(2);
                var rdToken = operands[0] as Token.Identifier;
                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");

                var opToken = operands[1] as Token.Number;
                if (opToken is null || !fitsInNBits(opToken.Value, 12))
                    throw new Exception($"{code}: expected a (12-bit) numerical value for the immediate operand, but got '{operands[1]}' instead");

                return new SyntheticInstruction.LoadImm(rd, (int)opToken.Value);
            }

            case InstrCode.mv:
            case InstrCode.neg:
            case InstrCode.not: {
                assertOperandCount(2);
                var rs1Token = operands[0] as Token.Identifier;
                var rs2Token = operands[1] as Token.Identifier;

                if (rs1Token is null || !TryGetRegisterValue(rs1Token.Text, out var rs1))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operands[0]}' instead");
                if (rs2Token is null || !TryGetRegisterValue(rs2Token.Text, out var rs2))
                    throw new Exception($"{code}: expected a register name for rs2, but got '{operands[1]}' instead");

                return new SyntheticInstruction.RegToReg(code, rs1, rs2);
            }

            case InstrCode.nop:
                assertOperandCount(0);
                return new SyntheticInstruction.Nop();
            case InstrCode.ret:
                assertOperandCount(0);
                return new SyntheticInstruction.Ret();

            case InstrCode.seqz:
            case InstrCode.snez: {
                assertOperandCount(2);
                var rdToken = operands[0] as Token.Identifier;
                var rsToken = operands[1] as Token.Identifier;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operands[0]}' instead");
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operands[1]}' instead");

                return new SyntheticInstruction.Set(code, rd, rs);
            }

            case InstrCode.fence:
            case InstrCode.fence_i:
            case InstrCode.ecall:
                assertOperandCount(0);
                return new Instruction.Immediate(code, 0, 0, 0b000000000000);

            case InstrCode.ebreak:
                assertOperandCount(0);
                return new Instruction.Immediate(code, 0, 0, 0b000000000001);

            default:
                throw new NotImplementedException(code.ToString());
        }

        bool fitsInNBits(long l, byte n) => l < (1 << n) && l > -((1 << (n-1)) + 1);

        // todo: this should not throw directly, because that means it won't ever be inlined,
        // which means the JIT might add a lot of bounds checks for no reason
        void assertOperandCount(int n) {
            if (operands.Length == n)
                return;
            throw new Exception($"{instrName.Text}: expected {n} operands, but got {operands.Length}");
        }
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
                    if (!tokens.TryPeek(out nextToken) || nextToken is not Token.Delimiter { Char: '(' }) {
                        operandTokens.Add(num);
                        break;
                    }

                    _ = tokens.Pop();

                    if (!tokens.TryPop(out nextToken) || nextToken is not Token.Identifier registerName)
                        throw new Exception("Unexpected token in offset-and-base expression, expected a register name");

                    if (!tokens.TryPop(out nextToken) || nextToken is not Token.Delimiter { Char: ')' })
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

    static bool TryGetRegisterValue(string str, out byte regIdx) {
        const byte INVALID_REG_IDX = 255;
        regIdx = INVALID_REG_IDX;

        if (str.Length < 2)
            return false;

        byte parseRegIdx()
            => Byte.TryParse(str.AsSpan(1), out var num)
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
                ? ~(val >> (bits + 1)) // signed has a bit less, but maybe the value is unsigned,
                : (val >>> bits);    // in which case it has the original bit available

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
}