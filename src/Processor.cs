using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokei;

public partial class Processor
{
    public int[] OldRegisters = new int[32];
    public int[] Registers = new int[32];
    public int PC;
    public byte[] Memory;

    public Instruction CurrentInstruction
        => Decoder.Decode(MemoryMarshal.Cast<byte, uint>(Memory)[PC/4]);

    public Processor(ReadOnlySpan<byte> initMemory) {
        Memory = initMemory.ToArray();
    }

    private void EndCycle(bool updatePC) {
        Registers[0] = 0;
        if (updatePC)
            PC += 4;
    }

    internal void PrintState() {
        var startInstr = Math.Max(0, PC/4 - 8);
        var length = Math.Min(Memory.Length / 4 - startInstr, 16);
        Disassembler.PrintDisassembly(Memory, startInstr, length, PC/4);

        Console.WriteLine();
        Console.WriteLine("Registers:");
        Console.WriteLine("\t PC = 0x" + PC.ToString("x").PadLeft(8, '0'));
        for (int i = 0; i < Registers.Length/4; i++) {
            for (int j = 0; j < 4; j++) {
                var regId = i + 8*j;
                Console.Write($"\t");
                Console.Write(("x" + regId).PadLeft(3));
                Console.Write(" = ");
                if (Registers[regId] != OldRegisters[regId])
                    Console.Write("\x1b[1m");
                Console.Write("0x" + Registers[regId].ToString("x").PadLeft(8, '0'));
                Console.Write("\x1b[0m");
            }
            Console.WriteLine();
        }
    }

    private void CheckOOB(int addr, int accessLength = 1) {
        if (addr < 0)
            throw new IndexOutOfRangeException("Tried to access memory in the negative range");
        if (addr + accessLength - 1 >= Memory.Length)
            throw new IndexOutOfRangeException($"Tried to access {accessLength} bytes at address 0x{addr.ToString("x").PadLeft(8, '0')}, resulting in OOB access");
    }

    private void CheckAlignement(Instruction instr) {
        switch (instr) {
            case Instruction.Branch b:
                if (b.Offset % 4 != 0)
                    throw new Exception("Misaligned branch offset: " + b.Offset);
                break;
            case Instruction.Jump j:
                if (j.Offset % 4 != 0)
                    throw new Exception("Misaligned branch offset: " + j.Offset);
                break;
            default:
                return;
        }
    }
}