using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tokei;

public partial class Processor
{
    public long[] OldRegisters = new long[32];
    public long[] Registers = new long[32];
    public long PC;
    public Memory<byte> Memory;

    public Instruction CurrentInstruction
        => Decoder.Decode(ReadMemory<uint>(PC));

    // ctor to init from a pre-alloc'd memory
    private Processor(Memory<byte> mem) {
        Memory = mem;
        OldRegisters[0x2] = Registers[0x2] = mem.Length; // init sp
    }

    public Processor(ReadOnlySpan<byte> initMemory) : this(initMemory.ToArray().AsMemory()) {}

    private void EndCycle(bool updatePC) {
        Registers[0] = 0;
        if (updatePC)
            PC += 4;
    }

    internal void PrintState() {
        var startInstr = Math.Max(0, (PC / 4) - 8);
        var length = Math.Min((Memory.Length / 4) - startInstr, 16);

        Disassembler.PrintDisassembly(Memory.Span, (int)startInstr, (int)length, (int)(PC / 4));

        Console.WriteLine();
        Console.WriteLine("Registers:");
        Console.WriteLine("\t PC = 0x" + PC.ToString("x").PadLeft(16, '0'));
        for (int i = 0; i < Registers.Length/4; i++) {
            for (int j = 0; j < 4; j++) {
                var regId = i + 8*j;
                Console.Write($"\t");
                Console.Write(("x" + regId).PadLeft(3));
                Console.Write(" = ");
                if (Registers[regId] != OldRegisters[regId])
                    Console.Write("\x1b[1m");
                Console.Write("0x" + Registers[regId].ToString("x").PadLeft(16, '0'));
                Console.Write("\x1b[0m");
            }
            Console.WriteLine();
        }
    }

    private T ReadMemory<T>(long longAddr) where T : struct {
        var accessLength = Unsafe.SizeOf<T>();
        var addr = (int)longAddr;

        if (longAddr < 0)
            throw new AccessViolationException("Tried to access memory in the negative range");
        if (longAddr <= Memory.Length) {
            var mem = Memory.Span[addr..];

            if (accessLength <= mem.Length)
                return MemoryMarshal.Read<T>(mem);
        }

        throw new AccessViolationException($"Tried to read a {typeof(T).Name} ({accessLength} bytes) at address 0x{addr.ToString("x").PadLeft(8, '0')}, but memory boundary is at 0x{Memory.Length.ToString("x").PadLeft(8, '0')}");
    }

    private void WriteMemory<T>(long longAddr, T value) where T : struct {
        var accessLength = Unsafe.SizeOf<T>();
        var addr = (int)longAddr;

        if (longAddr < 0)
            throw new AccessViolationException("Tried to access memory in the negative range");
        if (longAddr <= Memory.Length) {
            var mem = Memory.Span[addr..];

            if (accessLength < mem.Length) {
                MemoryMarshal.Write(mem, value);
                return;
            }
        }

        throw new AccessViolationException($"Tried to write a {typeof(T).Name} ({accessLength} bytes) at address 0x{addr.ToString("x").PadLeft(8, '0')}, but memory boundary is at 0x{Memory.Length.ToString("x").PadLeft(8, '0')}");
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