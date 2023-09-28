namespace Tokei;

public static class Decoder
{
    private const uint OPCODE_MASK = (uint)0b1111111;
    private const uint RD_MASK = (uint)0b11111 << 7;
    private const uint FUNCT3_MASK = (uint)0b111 << 12;
    private const uint RS1_MASK = (uint)0b11111 << 15;
    private const uint RS2_MASK = (uint)0b11111 << 20;
    private const uint FUNCT7_MASK = (uint)0b1111111 << 25;
    private const uint IMM_I_MASK = FUNCT7_MASK + RS2_MASK;
    private const uint IMM_U_MASK = ~(RD_MASK + OPCODE_MASK);

    private static byte GetOpcode(uint rawInstr)
        => (byte)(rawInstr & OPCODE_MASK);

    private static byte GetRd(uint rawInstr)
        => (byte)((rawInstr & RD_MASK) >> 7);
    private static byte GetRs1(uint rawInstr)
        => (byte)((rawInstr & RS1_MASK) >> 15);
    private static byte GetRs2(uint rawInstr)
        => (byte)((rawInstr & RS2_MASK) >> 20);

    private static int SignExtend(uint val, byte signIdx) {
        if (((val & ((uint)0b1 << signIdx)) >> signIdx) == 0)
            return (int)val;

        // int(-1) = uint(32'b1), so once shifted, upper will be full of 1's,
        // and lower will have the original value
        return (int)((uint)(-1 << signIdx) | val);
    }

    private static int GetImmediate_I(uint rawInstr)
        => SignExtend((rawInstr & IMM_I_MASK) >> 20, signIdx: 11);
    private static int GetImmediate_S(uint rawInstr)
        => SignExtend(((rawInstr >> 25) << 5) | GetRd(rawInstr), signIdx: 11);
    private static int GetImmediate_SB(uint rawInstr)
        => SignExtend((((rawInstr & ((uint)0b1 << 31)) >> 31) << 12)
         | (((rawInstr & ((uint)0b1 << 7)) >> 7) << 11)
         | (((rawInstr & ((uint)0b111111 << 25)) >> 25) << 5)
         | (((rawInstr & ((uint)0b1111 << 8)) >> 8) << 1), signIdx: 12);
    private static int GetImmediate_U(uint rawInstr)
        => SignExtend(rawInstr & IMM_U_MASK, signIdx: 31); // no need to shift since it's for Upper bits
    private static int GetImmediate_UJ(uint rawInstr)
        => SignExtend((((rawInstr & ((uint)0b1 << 31)) >> 31) << 20)
         | (((rawInstr & ((uint)0b1111111111 << 21)) >> 21) << 1)
         | (((rawInstr & ((uint)0b1 << 20)) >> 20) << 11)
         | (((rawInstr & ((uint)0b11111111 << 12)) >> 12) << 12), signIdx: 20);

    private static byte GetFunct3(uint rawInstr)
        => (byte)((rawInstr & FUNCT3_MASK) >> 12);
    private static byte GetFunct7(uint rawInstr)
        => (byte)(rawInstr >> 25);

    public static Instruction Decode(uint rawInstr) {
        byte opcode = GetOpcode(rawInstr);

        switch (opcode) {
            case 0b0000011:
                return DecodeLoad(rawInstr);
            case 0b0100011:
                return DecodeStore(rawInstr);
            case 0b0110011:
                return DecodeRegArith(rawInstr);
            case 0b0111011:
                return DecodeRegArithWord(rawInstr);
            case 0b1100011:
                return DecodeBranch(rawInstr);
            case 0b0010011:
                return DecodeImmArith(rawInstr);
            case 0b0011011:
                return DecodeImmArithWord(rawInstr);
            case 0b1100111: {
                var rd = GetRd(rawInstr);
                var rs = GetRs1(rawInstr);
                var imm = GetImmediate_I(rawInstr);
                return new Instruction.Immediate(InstrCode.jalr, rd, rs, imm);
            }
            case 0b1101111: {
                var rd = GetRd(rawInstr);
                var imm = GetImmediate_UJ(rawInstr);
                return new Instruction.Jump(InstrCode.jal, rd, imm);
            }
            case 0b1110011: {
                var rd = GetRd(rawInstr);
                var rs = GetRs1(rawInstr);
                var imm = GetImmediate_I(rawInstr);
                var code = imm switch {
                    0b00000000000 => InstrCode.ecall,
                    0b00000000001 => InstrCode.ebreak,
                    _ => throw InvalidInstruction(rawInstr)
                };
                return new Instruction.Immediate(code, rd, rs, imm);
            }
            case 0b0010111: {
                var rd = GetRd(rawInstr);
                var imm = GetImmediate_U(rawInstr);
                return new Instruction.UpperImmediate(InstrCode.auipc, rd, imm);
            }
            case 0b0110111: {
                var rd = GetRd(rawInstr);
                var imm = GetImmediate_U(rawInstr);
                return new Instruction.UpperImmediate(InstrCode.lui, rd, imm);
            }
            case 0b0001111: {// fence / fence.i
                return GetFunct3(rawInstr) == 0
                    ? new Instruction.Immediate(InstrCode.fence, 0, 0, 0)
                    : new Instruction.Immediate(InstrCode.fence_i, 0, 0, 0);
            }
            default:
                throw InvalidInstruction(rawInstr);
        }

    }

    private static Instruction.Immediate DecodeLoad(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b0000011);

        var rd = GetRd(rawInstr);
        var rs = GetRs1(rawInstr);
        var imm = GetImmediate_I(rawInstr);

        InstrCode code = GetFunct3(rawInstr) switch {
            0b000 => InstrCode.lb,
            0b001 => InstrCode.lh,
            0b010 => InstrCode.lw,
            0b011 => InstrCode.ld,
            0b100 => InstrCode.lbu,
            0b101 => InstrCode.lhu,
            0b110 => InstrCode.lwu,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rd, rs, imm);
    }

    private static Instruction.Store DecodeStore(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b0100011);

        var rs1 = GetRs1(rawInstr);
        var rs2 = GetRs2(rawInstr);
        var imm = GetImmediate_S(rawInstr);

        InstrCode code = GetFunct3(rawInstr) switch {
            0b000 => InstrCode.sb,
            0b001 => InstrCode.sh,
            0b010 => InstrCode.sw,
            0b011 => InstrCode.sd,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rs1, rs2, imm);
    }

    private static Instruction.Register DecodeRegArith(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b0110011);

        var rd = GetRd(rawInstr);
        var rs1 = GetRs1(rawInstr);
        var rs2 = GetRs2(rawInstr);

        var f3 = GetFunct3(rawInstr);
        var f7 = GetFunct7(rawInstr);

        InstrCode code = (f3, f7) switch {
            (0b000, 0b0000000) => InstrCode.add,
            (0b000, 0b0100000) => InstrCode.sub,
            (0b001, 0b0000000) => InstrCode.sll,
            (0b010, 0b0000000) => InstrCode.slt,
            (0b011, 0b0000000) => InstrCode.sltu,
            (0b100, 0b0000000) => InstrCode.xor,
            (0b101, 0b0000000) => InstrCode.srl,
            (0b101, 0b0100000) => InstrCode.sra,
            (0b110, 0b0000000) => InstrCode.or,
            (0b111, 0b0000000) => InstrCode.and,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rd, rs1, rs2);
    }

    private static Instruction.Register DecodeRegArithWord(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b0111011);

        var rd = GetRd(rawInstr);
        var rs1 = GetRs1(rawInstr);
        var rs2 = GetRs2(rawInstr);

        var f3 = GetFunct3(rawInstr);
        var f7 = GetFunct7(rawInstr);

        InstrCode code = (f3, f7) switch {
            (0b000, 0b0000000) => InstrCode.addw,
            (0b000, 0b0100000) => InstrCode.subw,
            (0b001, 0b0000000) => InstrCode.sllw,
            (0b101, 0b0000000) => InstrCode.srlw,
            (0b101, 0b0100000) => InstrCode.sraw,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rd, rs1, rs2);
    }

    private static Instruction.Branch DecodeBranch(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b1100011);

        var rs1 = GetRs1(rawInstr);
        var rs2 = GetRs2(rawInstr);
        var imm = GetImmediate_SB(rawInstr);

        InstrCode code = GetFunct3(rawInstr) switch {
            0b000 => InstrCode.beq,
            0b001 => InstrCode.bne,
            0b100 => InstrCode.blt,
            0b101 => InstrCode.bge,
            0b110 => InstrCode.bltu,
            0b111 => InstrCode.bgeu,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rs1, rs2, imm);
    }

    private static Instruction.Immediate DecodeImmArith(uint rawInstr) {
        Debug.Assert(GetOpcode(rawInstr) == 0b0010011);

        var rd = GetRd(rawInstr);
        var rs1 = GetRs1(rawInstr);
        var imm = GetImmediate_I(rawInstr);

        var f3 = GetFunct3(rawInstr);
        var f7 = GetFunct7(rawInstr);

        InstrCode code = f3 switch {
            0b000 => InstrCode.addi,
            0b001 => f7 switch { 0b0000000 => InstrCode.slli, _ => throw InvalidInstruction(rawInstr) },
            0b010 => InstrCode.slti,
            0b011 => InstrCode.sltiu,
            0b100 => InstrCode.xori,
            0b101 => f7 switch {
                        0b0000000 => InstrCode.srli,
                        0b0100000 => InstrCode.srai,
                        _ => throw InvalidInstruction(rawInstr)
                    },
            0b110 => InstrCode.ori,
            0b111 => InstrCode.andi,
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rd, rs1, imm);
    }

    private static Instruction.Immediate DecodeImmArithWord(uint rawInstr)
    {
        Debug.Assert(GetOpcode(rawInstr) == 0b0010011);

        var rd = GetRd(rawInstr);
        var rs1 = GetRs1(rawInstr);
        var imm = GetImmediate_I(rawInstr);

        var f3 = GetFunct3(rawInstr);
        var f7 = GetFunct7(rawInstr);

        InstrCode code = f3 switch
        {
            0b000 => InstrCode.addiw,
            0b001 => f7 switch { 0b0000000 => InstrCode.slliw, _ => throw InvalidInstruction(rawInstr) },
            0b101 => f7 switch {
                        0b0000000 => InstrCode.srliw,
                        0b0100000 => InstrCode.sraiw,
                        _ => throw InvalidInstruction(rawInstr)
                    },
            _ => throw InvalidInstruction(rawInstr)
        };

        return new(code, rd, rs1, imm);
    }

    // todo: improve msg with eg opcode+funct and if possible type
    private static Exception InvalidInstruction(uint rawInstr) {
        var opcode = Convert.ToString(GetOpcode(rawInstr), toBase: 2).PadLeft(7, '0');
        var rd = Convert.ToString(GetRd(rawInstr), toBase: 2).PadLeft(5, '0');
        var f3 = Convert.ToString(GetFunct3(rawInstr), toBase: 2).PadLeft(3, '0');
        var rs1 = Convert.ToString(GetRs1(rawInstr), toBase: 2).PadLeft(5, '0');
        var rs2 = Convert.ToString(GetRs2(rawInstr), toBase: 2).PadLeft(5, '0');
        var f7 = Convert.ToString(GetFunct7(rawInstr), toBase: 2).PadLeft(7, '0');
        return new Exception($"Could not decode instruction '{f7}|{rs2}|{rs1}|{f3}|{rd}|{opcode}' (0x{rawInstr:x})");
    }
}