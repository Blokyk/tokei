namespace Tokei;

public static partial class Assembler
{
    private static uint Encode(Instruction instr) {
        var code = instr.Code;

        var opcode = GetOpcode(code);
        switch (instr) {
            case Instruction.Register r: {
                var f3 = GetFunct3(code);
                var f7 = GetFunct7(code);
                return (uint)(opcode
                    | (r.Rd << 7)
                    | (f3 << 12)
                    | (r.Rs1 << 15)
                    | (r.Rs2 << 20)
                    | (f7 << 25)
                );
            }
            case Instruction.Immediate i: {
                var f3 = GetFunct3(code);
                return (uint)(opcode
                    | (i.Rd << 7)
                    | (f3 << 12)
                    | (i.Rs << 15)
                    | (i.Operand << 20)
                );
            }
            case Instruction.Store s: {
                const int IMM_MASK = 0b11111;
                var f3 = GetFunct3(code);
                var lowerImm = s.Offset & IMM_MASK;
                var upperImm = (s.Offset & ~IMM_MASK) >>> 5;
                return (uint)(opcode
                    | (lowerImm << 7)
                    | (f3 << 12)
                    | (s.Rbase << 15)
                    | (s.Rs << 20)
                    | (upperImm << 25)
                );
            }
            case Instruction.Branch b: {
                var f3 = GetFunct3(code);
                var b11 = (b.Offset   & 0b0100000000000) >>> 11;
                var b4_1 = (b.Offset  & 0b0000000011110) >>> 1;
                var b10_5 = (b.Offset & 0b0011111100000) >>> 5;
                var b12 = (b.Offset   & 0b1000000000000) >>> 12;
                return (uint)(opcode
                    | (b11 << 7)
                    | (b4_1 << 8)
                    | (f3 << 12)
                    | (b.Rs1 << 15)
                    | (b.Rs2 << 20)
                    | (b10_5 << 25)
                    | (b12 << 31)
                );
            }
            case Instruction.UpperImmediate u: {
                return (uint)(opcode | (u.Rd << 7) | (u.Operand << 12));
            }
            case Instruction.Jump j: {
                var b19_12 = (j.Offset & 0b00000000000011111111000000000000) >>> 12;
                var b11 = (j.Offset    & 0b00000000000000000000100000000000) >>> 11;
                var b10_1 = (j.Offset  & 0b00000000000000000000011111111110) >>> 1;
                var b20 = (j.Offset    & 0b00000000000100000000000000000000) >>> 20;
                return (uint)(opcode
                    | (j.Rd << 7)
                    | (b19_12 << 12)
                    | (b11 << 20)
                    | (b10_1 << 21)
                    | (b20 << 31)
                );
            }
            default:
                throw new Exception($"Couldn't encode instruction '{instr}'");
        }
    }

    private static byte GetOpcode(InstrCode code) {
        switch (code) {
            case InstrCode.lb:
            case InstrCode.lh:
            case InstrCode.lw:
            case InstrCode.ld:
            case InstrCode.lbu:
            case InstrCode.lhu:
            case InstrCode.lwu:
                return 0b0000011;
            case InstrCode.fence:
            case InstrCode.fence_i:
                return 0b0001111;
            case InstrCode.addi:
            case InstrCode.slli:
            case InstrCode.slti:
            case InstrCode.sltiu:
            case InstrCode.xori:
            case InstrCode.srli:
            case InstrCode.srai:
            case InstrCode.ori:
            case InstrCode.andi:
                return 0b0010011;
            case InstrCode.auipc:
                return 0b0010111;
            case InstrCode.sb:
            case InstrCode.sh:
            case InstrCode.sw:
            case InstrCode.sd:
                return 0b0100011;
            case InstrCode.add:
            case InstrCode.sub:
            case InstrCode.sll:
            case InstrCode.slt:
            case InstrCode.sltu:
            case InstrCode.xor:
            case InstrCode.srl:
            case InstrCode.sra:
            case InstrCode.or:
            case InstrCode.and:
                return 0b0110011;
            case InstrCode.lui:
                return 0b0110111;
            case InstrCode.beq:
            case InstrCode.bne:
            case InstrCode.blt:
            case InstrCode.bge:
            case InstrCode.bltu:
            case InstrCode.bgeu:
                return 0b1100011;
            case InstrCode.jalr:
                return 0b1100111;
            case InstrCode.jal:
                return 0b1101111;
            case InstrCode.ecall:
            case InstrCode.ebreak:
                return 0b1110011;
            default:
                throw new NotImplementedException($"Encoding instruction '{code}' isn't implemented yet");
        };
    }

    private static byte GetFunct3(InstrCode code) {
        switch (code) {
            case InstrCode.lb:
            case InstrCode.fence:
            case InstrCode.addi:
            case InstrCode.add:
            case InstrCode.sub:
            case InstrCode.beq:
            case InstrCode.jalr:
            case InstrCode.ecall:
            case InstrCode.ebreak:
                return 0b000;
            case InstrCode.lh:
            case InstrCode.fence_i:
            case InstrCode.slli:
            case InstrCode.sll:
            case InstrCode.bne:
                return 0b001;
            case InstrCode.lw:
            case InstrCode.slti:
            case InstrCode.slt:
            case InstrCode.sw:
                return 0b010;
            case InstrCode.ld:
            case InstrCode.sltiu:
            case InstrCode.sd:
            case InstrCode.sltu:
                return 0b011;
            case InstrCode.lbu:
            case InstrCode.xori:
            case InstrCode.xor:
            case InstrCode.blt:
                return 0b100;
            case InstrCode.lhu:
            case InstrCode.srli:
            case InstrCode.srai:
            case InstrCode.srl:
            case InstrCode.sra:
            case InstrCode.bge:
                return 0b101;
            case InstrCode.lwu:
            case InstrCode.ori:
            case InstrCode.or:
            case InstrCode.bltu:
                return 0b110;
            case InstrCode.andi:
            case InstrCode.and:
            case InstrCode.bgeu:
                return 0b111;
            default:
                throw new Exception($"Instruction '{code}' doesn't have a corresponding funct3 code");
        }
    }

    private static byte GetFunct7(InstrCode code) {
        switch (code) {
            case InstrCode.srai:
            case InstrCode.sub:
            case InstrCode.sra:
                return 0b0100000;
            default:
                return 0b0000000;
        }
    }

}