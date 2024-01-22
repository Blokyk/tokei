using static Tokei.Instruction;

namespace Tokei;

public partial class Processor
{
    public Processor Clone(bool cloneMemory = true) {
        var newProcessor = new Processor(cloneMemory ? Memory.ToArray() : Memory);

        OldRegisters.CopyTo(newProcessor.OldRegisters.AsSpan());
        Registers.CopyTo(newProcessor.Registers.AsSpan());
        newProcessor.PC = PC;

        return newProcessor;
    }

    public bool Step() {
        Array.Copy(Registers, OldRegisters, Registers.Length);

        // if we're at the end of memory, just stop
        if (PC == Memory.Length)
            return false;

        // CurrentInstruction will take care of raising an exception for us
        var instr = CurrentInstruction;

        long oldPC = PC; // used to check if we're in a loop
        bool changedPC = false;

        CheckAlignement(instr);
        // todo: control status registers
        switch (instr) {
            // --------------------\\
            //                     \\
            // REGISTER ARITHMETIC \\
            //                     \\
            //---------------------\\

            // add rd, rs1, rs2
            case Register { Code: InstrCode.add } add:
                Registers[add.Rd] = Registers[add.Rs1] + Registers[add.Rs2];
                break;
            // sub rd, rs1, rs2
            case Register { Code: InstrCode.sub } sub:
                Registers[sub.Rd] = Registers[sub.Rs1] - Registers[sub.Rs2];
                break;

            // and rd, rs1, rs2
            case Register { Code: InstrCode.and } and:
                Registers[and.Rd] = Registers[and.Rs1] & Registers[and.Rs2];
                break;
            // or rd, rs1, rs2
            case Register { Code: InstrCode.or } or:
                Registers[or.Rd] = Registers[or.Rs1] | Registers[or.Rs2];
                break;
            // xor rd, rs1, rs2
            case Register { Code: InstrCode.xor } xor:
                Registers[xor.Rd] = Registers[xor.Rs1] ^ Registers[xor.Rs2];
                break;
            // sra rd, rs1, rs2
            case Register { Code: InstrCode.sra } sra:
                Registers[sra.Rd] = Registers[sra.Rs1] >> (byte)(Registers[sra.Rs2] & 0b11111);
                break;
            // srl rd, rs1, rs2
            case Register { Code: InstrCode.srl } srl:
                Registers[srl.Rd] = Registers[srl.Rs1] >>> (byte)(Registers[srl.Rs2] & 0b11111);
                break;
            // sll rd, rs1, rs2
            case Register { Code: InstrCode.sll } sll:
                Registers[sll.Rd] = Registers[sll.Rs1] << (byte)(Registers[sll.Rs2] & 0b11111);
                break;

            // slt rd, rs1, rs2
            case Register { Code: InstrCode.slt } slt:
                Registers[slt.Rd]
                    = Registers[slt.Rs1] < Registers[slt.Rs2]
                    ? 1 : 0;
                break;
            // sltu rd, rs1, rs2
            case Register { Code: InstrCode.sltu } sltu:
                Registers[sltu.Rd]
                    = (ulong)Registers[sltu.Rs1] < (ulong)Registers[sltu.Rs2]
                    ? 1 : 0;
                break;

            // ---------------------\\
            //                      \\
            // IMMEDIATE ARITHMETIC \\
            //                      \\
            //----------------------\\

            // addi rd, rs, imm
            case Immediate { Code: InstrCode.addi } addi:
                Registers[addi.Rd] = Registers[addi.Rs] + addi.Operand;
                break;

            // andi rd, rs, imm
            case Immediate { Code: InstrCode.andi } andi:
                Registers[andi.Rd] = Registers[andi.Rs] & andi.Operand;
                break;
            // ori rd, rs1, rs2
            case Immediate { Code: InstrCode.ori } ori:
                Registers[ori.Rd] = Registers[ori.Rs] | (long)ori.Operand;
                break;
            // xori rd, rs1, rs2
            case Immediate { Code: InstrCode.xori } xori:
                Registers[xori.Rd] = Registers[xori.Rs] ^ xori.Operand;
                break;
            // srai rd, rs1, imm
            case Immediate { Code: InstrCode.srai } srai:
                Registers[srai.Rd] = Registers[srai.Rs] >> srai.Operand;
                break;
            // srli rd, rs1, imm
            case Immediate { Code: InstrCode.srli } srli:
                Registers[srli.Rd] = Registers[srli.Rs] >>> srli.Operand;
                break;
            // slli rd, rs1, imm
            case Immediate { Code: InstrCode.slli } slli:
                Registers[slli.Rd] = Registers[slli.Rs] << slli.Operand;
                break;

            // slti rd, rs1, imm
            case Immediate { Code: InstrCode.slti } slti:
                Registers[slti.Rd]
                    = Registers[slti.Rs] < slti.Operand
                    ? 1 : 0;
                break;
            // sltiu rd, rs1, imm
            case Immediate { Code: InstrCode.sltiu } sltiu:
                Registers[sltiu.Rd]
                    = (ulong)Registers[sltiu.Rs] < (ulong)sltiu.Operand
                    ? 1 : 0;
                break;


            // -----------------\\
            //                  \\
            // BRANCHES & JUMPS \\
            //                  \\
            //------------------\\

            // beq rs1, rs2, offset
            case Branch { Code: InstrCode.beq } beq:
                if (Registers[beq.Rs1] != Registers[beq.Rs2])
                    break;
                PC += beq.Offset;
                changedPC = true;
                break;
            // bge rs1, rs2, offset
            case Branch { Code: InstrCode.bge } bge:
                if (Registers[bge.Rs1] < Registers[bge.Rs2])
                    break;
                PC += bge.Offset;
                changedPC = true;
                break;
            // bgeu rs1, rs2, offset
            case Branch { Code: InstrCode.bgeu } bgeu:
                if ((ulong)Registers[bgeu.Rs1] < (ulong)Registers[bgeu.Rs2])
                    break;
                PC += bgeu.Offset;
                changedPC = true;
                break;
            // blt rs1, rs2, offset
            case Branch { Code: InstrCode.blt } blt:
                if (Registers[blt.Rs1] >= Registers[blt.Rs2])
                    break;
                PC += blt.Offset;
                changedPC = true;
                break;
            // bltu rs1, rs2, offset
            case Branch { Code: InstrCode.bltu } bltu:
                if ((ulong)Registers[bltu.Rs1] >= (ulong)Registers[bltu.Rs2])
                    break;
                PC += bltu.Offset;
                changedPC = true;
                break;
            // bne rs1, rs2, offset
            case Branch { Code: InstrCode.bne } bne:
                if (Registers[bne.Rs1] == Registers[bne.Rs2])
                    break;
                PC += bne.Offset;
                changedPC = true;
                break;

            // jal rd, imm
            case Jump { Code: InstrCode.jal } jal:
                Registers[jal.Rd] = PC + 4;
                PC += jal.Offset;
                changedPC = true;
                break;
            // jalr rd, rs, offset
            case Immediate { Code: InstrCode.jalr } jalr:
                Registers[jalr.Rd] = PC + 4;
                PC = Registers[jalr.Rs] + jalr.Operand;
                changedPC = true;
                break;
            // auipc rd, up_imm
            case UpperImmediate { Code: InstrCode.auipc } auipc:
                Registers[auipc.Rd] = PC + auipc.Operand;
                break;


            // ------------------\\
            //                   \\
            // LOADING & STORING \\
            //                   \\
            //-------------------\\

            // lb rd, rs, offset
            case Immediate { Code: InstrCode.lb } lb: {
                var addr = Registers[lb.Rs] + lb.Operand;
                Registers[lb.Rd] = ReadMemory<sbyte>(addr);
                break;
            }
            // lbu rd, rs, offset
            case Immediate { Code: InstrCode.lbu } lbu: {
                var addr = Registers[lbu.Rs] + lbu.Operand;
                Registers[lbu.Rd] = ReadMemory<byte>(addr);
                break;
            }
            // lh rd, rs, offset
            case Immediate { Code: InstrCode.lh } lh: {
                var addr = Registers[lh.Rs] + lh.Operand;
                Registers[lh.Rd] = ReadMemory<short>(addr);
                break;
            }
            // lhu rd, rs, offset
            case Immediate { Code: InstrCode.lhu } lhu: {
                var addr = Registers[lhu.Rs] + lhu.Operand;
                Registers[lhu.Rd] = ReadMemory<ushort>(addr);
                break;
            }
            // lw rd, rs, offset
            case Immediate { Code: InstrCode.lw } lw: {
                var addr = Registers[lw.Rs] + lw.Operand;
                Registers[lw.Rd] = ReadMemory<int>(addr);
                break;
            }
            // lwu rd, rs, offset
            case Immediate { Code: InstrCode.lwu } lwu: {
                var addr = Registers[lwu.Rs] + lwu.Operand;
                Registers[lwu.Rd] = ReadMemory<uint>(addr);
                break;
            }
            // ld rd, rs, offset
            case Immediate { Code: InstrCode.ld } ld: {
                var addr = Registers[ld.Rs] + ld.Operand;
                Registers[ld.Rd] = ReadMemory<long>(addr);
                break;
            }
            // lui rd, imm
            case UpperImmediate { Code: InstrCode.lui } lui:
                Registers[lui.Rd] = lui.Operand;
                break;

            // sb rs, rb(offset)
            case Store { Code: InstrCode.sb } sb: {
                var addr = Registers[sb.Rbase] + sb.Offset;
                WriteMemory(addr, (byte)Registers[sb.Rs]);
                break;
            }
            // sh rs, rb(offset)
            case Store { Code: InstrCode.sh } sh: {
                var addr = Registers[sh.Rbase] + sh.Offset;
                WriteMemory(addr, (short)Registers[sh.Rs]);
                break;
            }
            // sw rs, rb(offset)
            case Store { Code: InstrCode.sw } sw: {
                var addr = Registers[sw.Rbase] + sw.Offset;
                WriteMemory(addr, (int)Registers[sw.Rs]);
                break;
            }
            // sd rs, rb(offset)
            case Store { Code: InstrCode.sd } sd: {
                var addr = Registers[sd.Rbase] + sd.Offset;
                WriteMemory(addr, (long)Registers[sd.Rs]);
                break;
            }

            //
            // unimplemented
            //
            // ebreak
            case Immediate { Code: InstrCode.ebreak }:
                Console.WriteLine("Debugger break!");
                Debugger.Break();
                break;
            // ecall
            case Immediate { Code: InstrCode.ecall }:
                Console.WriteLine("syscall");
                break;
            // fence/fence.i
            case Immediate { Code: InstrCode.fence or InstrCode.fence_i }:
                Console.WriteLine("fence/fence.i received");
                break;
            case Error { RawInstruction: 0x0 }: // completely stop on 0x0
                return false;
            case Error e:
                throw new InvalidInstructionException(e.RawInstruction);
            default:
                throw new NotImplementedException(instr.ToString());
        }

        // if we're in an infinite loop
        if (changedPC && oldPC == PC)
            return false;

        EndCycle(!changedPC);
        return true;
    }
}