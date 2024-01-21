using System.Runtime.InteropServices;

namespace Tokei;

public class InstructionGenerator
{
    public static readonly InstructionGenerator Shared = new();

    public readonly int Seed;

    private Random _rng;
    public InstructionGenerator() : this(Random.Shared.Next()) {}
    public InstructionGenerator(int seed) {
        Console.Error.WriteLine($"Using seed {seed} for instr-gen rng");
        Seed = seed;
        _rng = new Random(Seed);
    }

    public Instruction[] GetRandomExecutableInstructions(int instrCount, out Processor finalState) {
        // create a new array filled with random instrs
        var instrs = new Instruction[instrCount];

        finalState = default!;

        if (instrCount > 0x4000/sizeof(uint)) {
            throw new InvalidOperationException(
                $"Too many instructions requested. Maximum possible is {0x4000/sizeof(uint)}"
            );
        }

        var loader = new Instruction[5];

        var mem = new uint[0x4000 / sizeof(uint)];

        int crashes = 1;
        bool crashed;
        do {
            Console.Error.WriteLine();
            Console.Error.WriteLine("New generation...");

            Array.Clear(mem);

            for (int i = 0; i < loader.Length; i++) {
                loader[i] = new Instruction.Immediate(InstrCode.addi, RandomReg(), 0, RandomImm());
                mem[i] = Encoder.Encode(loader[i]);
            }

            for (int i = 0; i < instrCount; i++) {
                instrs[i] = Next();
                mem[loader.Length+i] = Encoder.Encode(instrs[i]);
            }

            var cpu = new Processor(MemoryMarshal.Cast<uint, byte>(mem));

            try {
                int cycles = -loader.Length; // ignore loader instructions when counting cycles
                do {
                    var currInstr = cpu.CurrentInstruction;
                    Console.Error.WriteLine(Disassembler.FormatInstruction(currInstr));

                    cycles++;
                } while (cpu.Step() && cycles < instrCount * 100);

                finalState = cpu;

                // if we didn't even execute as many instructions as were requested,
                // we probably just hit a NOP and stopped, which isn't very fun
                if (cycles < instrCount) {
                    crashes++;
                    crashed = true;
                    continue;
                } else if (cycles >= instrCount*100) {
                    // we might have been stuck in a loop, which for our purpose
                    // is as bad an outcome as a crash (even slightly worse!)
                    crashes++;
                    crashed = true;
                    continue;
                } else {
                    // otherwise, that means we ran mostly as "expected"
                    break;
                }
            } catch (AccessViolationException) {
                crashes++;
                crashed = true;
                continue;
            } catch (InvalidOperationException) {
                crashes++;
                crashed = true;
                continue;
            }
        } while (crashed);

        Console.Error.WriteLine($"Generating {instrCount} instructions took {crashes} tries.");
        return [.. loader, .. instrs];
    }

    public InstrCode RandomInstrCode() {
        InstrCode code;

        do {
            code = (InstrCode)_rng.Next(1, (int)InstrCode.jal + 1);
        } while (code is InstrCode.fence or InstrCode.fence_i or InstrCode.ebreak or InstrCode.ecall or InstrCode.lui or InstrCode.auipc); // those instr aren't implemented in the c emulator

        return code;
    }

    // 30% regs
    // 40% arithmetic
    // 10% load
    // 10% store
    // 5% branch
    // 5% jump
    public Instruction Next()
        => _rng.Next(0, 100) switch {
            < 30  => NextRegType(),
            < 70  => NextImmOperation(),
            < 80  => NextLoadOperation(),
            < 90  => NextStoreType(),
            < 95  => NextBranchType(),
            < 100 => NextJumpType(),
            _ => null!,
        };

    public Instruction NextRegType() {
        var code = RandomRegInstrCode();

        byte rd = RandomReg(), rs1 = RandomReg(), rs2 = RandomReg();
        return new Instruction.Register(code, rd, rs1, rs2);
    }

    public Instruction NextImmType() {
        var code = RandomImmInstrCode();

        byte rd = RandomReg(), rs = RandomReg();
        int imm
            = code.IsShortImm()
            ? _rng.Next(0, 1 << 5)
            : RandomImm();
        return new Instruction.Immediate(code, rd, rs, imm);
    }

    public Instruction NextImmOperation() {
        var code = RandomImmOpInstrCode();

        byte rd = RandomReg(), rs = RandomReg();
        int imm
            = code.IsShortImm()
            ? _rng.Next(0, 1 << 5)
            : RandomImm();
        return new Instruction.Immediate(code, rd, rs, imm);
    }

    public Instruction NextLoadOperation() {
        var code = RandomLoadOpInstrCode();

        byte rd = RandomReg(), rs = RandomReg();
        int imm
            = code.IsShortImm()
            ? _rng.Next(0, 1 << 5)
            : RandomImm();
        return new Instruction.Immediate(code, rd, rs, imm);
    }

    public Instruction NextStoreType() {
        var code = RandomStoreInstrCode();

        byte rd = RandomReg(), rs = RandomReg();
        int imm = RandomImm();
        return new Instruction.Store(code, rd, rs, imm);
    }

    public Instruction NextBranchType() {
        var code = RandomBranchInstrCode();

        byte rd = RandomReg(), rs = RandomReg();
        int imm = RandomBranchOffset();
        return new Instruction.Branch(code, rd, rs, imm);
    }

    public Instruction NextJumpType() {
        var code = RandomJumpInstrCode();

        byte rd = RandomReg();
        int imm = RandomJumpOffset();
        return new Instruction.Jump(code, rd, imm);
    }

    int RandomSign() => _rng.Next(0, 2) == 0 ? 1 : -1;
    byte RandomReg() => (byte)_rng.Next(0, 32);
    int RandomImm() => RandomSign() * _rng.Next(1 << 11);
    int RandomBranchOffset() => RandomSign() * (_rng.Next(1 << 12) & ~0b11); // align to 4 bytes
    int RandomJumpOffset() => RandomSign() * (_rng.Next(1 << 20) & ~0b11); // align to 4 bytes

    public InstrCode RandomRegInstrCode()
        => (InstrCode)_rng.Next((int)InstrCode.add, (int)InstrCode.and + 1);
    public InstrCode RandomStoreInstrCode()
        => (InstrCode)_rng.Next((int)InstrCode.sb, (int)InstrCode.sd + 1);
    public InstrCode RandomBranchInstrCode()
        => (InstrCode)_rng.Next((int)InstrCode.beq, (int)InstrCode.bgeu + 1);
    // public InstrCode RandomUpperInstrCode()
    //     => (InstrCode)_rng.Next((int)InstrCode.auipc, (int)InstrCode.lui + 1);
    public InstrCode RandomJumpInstrCode()
        => InstrCode.jal;

    public InstrCode RandomImmInstrCode() {
        InstrCode code;

        do {
            code = (InstrCode)_rng.Next((int)InstrCode.lb, (int)InstrCode.srai + 1);
        } while (code is InstrCode.fence or InstrCode.fence_i or InstrCode.ebreak or InstrCode.ecall); // those instr aren't implemented in the c emulator

        return code;
    }

    public InstrCode RandomImmOpInstrCode()
        => _rng.Next(0, 9) switch {
            < 6 => (InstrCode)_rng.Next((int)InstrCode.addi, (int)InstrCode.andi + 1),
            _  =>  (InstrCode)_rng.Next((int)InstrCode.slli, (int)InstrCode.srai + 1)
        };
    public InstrCode RandomLoadOpInstrCode()
        => (InstrCode)_rng.Next((int)InstrCode.lb, (int)InstrCode.lwu + 1);
}