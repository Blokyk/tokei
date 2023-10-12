namespace Tokei;

public static partial class Assembler
{
    private static Instruction ParseInstruction(Token.Identifier instrName, ref Stack<Token> tokens, out string? label) {
        label = null;

        var operandTokens = ParseInstructionOperands(ref tokens);

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
                var rdToken = operandTokens[0] as Token.Identifier;
                var rs1Token = operandTokens[1] as Token.Identifier;
                var rs2Token = operandTokens[2] as Token.Identifier;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operandTokens[0]}' instead");
                if (rs1Token is null || !TryGetRegisterValue(rs1Token.Text, out var rs1))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operandTokens[1]}' instead");
                if (rs2Token is null || !TryGetRegisterValue(rs2Token.Text, out var rs2))
                    throw new Exception($"{code}: expected a register name for rs2, but got '{operandTokens[2]}' instead");

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
                var rdToken = operandTokens[0] as Token.Identifier;
                var rsToken = operandTokens[1] as Token.Identifier;
                var opToken = operandTokens[2] as Token.Number;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operandTokens[0]}' instead");
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operandTokens[1]}' instead");
                if (opToken is null || opToken.Value is > 2047 or < -2048)
                    throw new Exception($"{code}: expected a (12-bit) numerical value for the immediate operand, but got '{operandTokens[2]}' instead");

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
                if (operandTokens.Length != 2 || operandTokens[1] is not Token.OffsetAndBase offsetBaseToken)
                    goto case InstrCode.addi;
                var rdToken = operandTokens[0] as Token.Identifier;
                var rsToken = offsetBaseToken.BaseRegister;
                var offset = offsetBaseToken.Offset.Value;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operandTokens[0]}' instead");
                if (!TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for the base, but got '{rsToken}' instead");
                if (offset >= 4096)
                    throw new Exception($"{code}: Offset '{offset}' is too large; consider loading a higher value into {rsToken}");

                return new Instruction.Immediate(code, rd, rs, (int)offset);
            }

            case InstrCode.auipc:
            case InstrCode.lui: {
                assertOperandCount(1);
                var rdToken = operandTokens[0] as Token.Identifier;
                var opToken = operandTokens[1] as Token.Number;

                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operandTokens[0]}' instead");
                if (opToken is null || opToken.Value is > 524287 or < -524288)
                    throw new Exception($"{code}: expected a (20-bit) numerical value for the immediate operand, but got '{operandTokens[2]}' instead");

                return new Instruction.UpperImmediate(code, rd, (int)opToken.Value);
            }

            case InstrCode.beq:
            case InstrCode.bge:
            case InstrCode.bgeu:
            case InstrCode.blt:
            case InstrCode.bltu:
            case InstrCode.bne: {
                assertOperandCount(3);
                var rs1Token = operandTokens[0] as Token.Identifier;
                var rs2Token = operandTokens[1] as Token.Identifier;

                if (rs1Token is null || !TryGetRegisterValue(rs1Token.Text, out var rs1))
                    throw new Exception($"{code}: expected a register name for rs1, but got '{operandTokens[0]}' instead");
                if (rs2Token is null || !TryGetRegisterValue(rs2Token.Text, out var rs2))
                    throw new Exception($"{code}: expected a register name for rs2, but got '{operandTokens[1]}' instead");

                var offset = ParseLabelOrOffset(operandTokens[2], 13, code, out label);
                return new Instruction.Branch(code, rs1, rs2, offset);
            }

            case InstrCode.jal: {
                assertOperandCount(2);
                var rdToken = operandTokens[0] as Token.Identifier;
                if (rdToken is null || !TryGetRegisterValue(rdToken.Text, out var rd))
                    throw new Exception($"{code}: expected a register name for rd, but got '{operandTokens[0]}' instead");
                var offset = ParseLabelOrOffset(operandTokens[1], 20, code, out label);
                return new Instruction.Jump(code, rd, offset);
            }

            case InstrCode.sb:
            case InstrCode.sd:
            case InstrCode.sh:
            case InstrCode.sw: {
                Token.Identifier? rbaseToken;
                Token.Identifier? rsToken;
                Token.Number? opToken;

                if (operandTokens.Length == 2 && operandTokens[1] is Token.OffsetAndBase offsetBaseToken) {
                    rbaseToken = offsetBaseToken.BaseRegister;
                    opToken = offsetBaseToken.Offset;
                } else {
                    assertOperandCount(3);
                    rbaseToken = operandTokens[0] as Token.Identifier;
                    opToken = operandTokens[2] as Token.Number;
                }

                rsToken = operandTokens[1] as Token.Identifier;

                if (rbaseToken is null || !TryGetRegisterValue(rbaseToken.Text, out var rbase))
                    throw new Exception($"{code}: expected a register name for rbase, but got '{operandTokens[0]}' instead");
                if (rsToken is null || !TryGetRegisterValue(rsToken.Text, out var rs))
                    throw new Exception($"{code}: expected a register name for rs, but got '{operandTokens[1]}' instead");
                if (opToken is null || opToken.Value is > 2047 or < -2048)
                    throw new Exception($"{code}: expected a (12-bit) numerical value for the immediate operand, but got '{operandTokens[2]}' instead");

                    return new Instruction.Store(code, rbase, rs, (int)opToken.Value);
            }

            case InstrCode.fence:
            case InstrCode.fence_i:
            case InstrCode.ecall:
                assertOperandCount(0);
                return new Instruction.Immediate(code, 0, 0, 0);

            case InstrCode.ebreak:
                assertOperandCount(0);
                return new Instruction.Immediate(code, 0, 0, 0b000000000001);

            default:
                throw new NotImplementedException(code.ToString());
        }

        // todo: this should not throw directly, because that means it won't ever be inlined,
        // which means the JIT might add a lot of bounds checks for no reason
        void assertOperandCount(int n) {
            if (operandTokens.Length == n)
                return;
            throw new Exception($"{instrName.Text}: expected {n} operands, but got {operandTokens.Length}");
        }
    }
}