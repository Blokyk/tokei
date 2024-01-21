using System.Runtime.InteropServices;
using System.Text;
using StarKid;

namespace Tokei;

[CommandGroup("tokei", ShortDesc = "A RISC-V toolchain, written in C#")]
internal static partial class TokeiApp
{
    internal static Stream ParseFileNameOrStdIn(string s) {
        if (s == "-")
            return Console.OpenStandardInput();

        if (!File.Exists(s))
            throw new FileNotFoundException(null, s);

        return File.OpenRead(s);
    }

    /// <summary>Assemble RISC-V assembly into raw machine code</summary>
    /// <param name="srcStream">Assembly file to convert</param>
    /// <param name="outputFile">Path to the output raw binary file</param>
    [Command("asm")]
    public static void Assemble(
        [ParseWith(nameof(ParseFileNameOrStdIn))]
        Stream srcStream,

        [Option("output", 'o')]
        [ValidateWith(nameof(CheckFileIsWritable))]
        FileInfo? outputFile
    ) {
        outputFile ??= new FileInfo("a.out");

        using var srcStreamReader = new StreamReader(srcStream);
        var srcText = srcStreamReader.ReadToEnd();

        var bytes = Assembler.Assemble(srcText);

        using var outputStream
            = outputFile.Exists
            ? outputFile.Open(FileMode.Truncate)
            : outputFile.Create();

        outputStream.Write(bytes);
    }

    [Command("gen-instrs")]
    public static void Generate(
        [ValidateWith(nameof(Int32.IsPositive))]
        int count,

        [Option("seed", 's')] int? seed,

        [Option("output", 'o')] FileInfo? outputFile
    ) {
        var generator
            = seed.HasValue
            ? new InstructionGenerator(seed.Value)
            : new();

        Console.WriteLine($"Generating {count} instruction(s)");

        var instrs = generator.GetRandomExecutableInstructions(count, out var finalState);

        if (outputFile is not null) {
            var sb = new StringBuilder();

            sb.Append("# Seed: ").Append(generator.Seed).AppendLine("\n");

            foreach (var instr in instrs) {
                sb.AppendLine(Disassembler.FormatInstruction(instr));
            }

            sb.AppendLine();
            sb.AppendLine("# EXPECTED");

            for (int i = 0; i < 32; i++) {
                var val = finalState.Registers[i];
                if (val != 0)
                    sb.Append("# x").Append(i).Append(": ").Append(val).AppendLine();
            }

            File.WriteAllText(outputFile.FullName, sb.ToString());
        } else {
            Disassembler.PrintDisassembly(instrs);
        }
    }

    public enum Endianness { Default, Big, Little = Default }

    /// <summary>
    /// Disassemble a raw RISC-V binary
    /// </summary>
    /// <param name="inputBinary"></param>
    /// <param name="endianness">Original endianness of the input file</param>
    /// <param name="byteOffset">Offset, in bytes, at which the instructions actually start in the input file</param>
    [Command("disasm")]
    public static void Disassemble(
        [ParseWith(nameof(ParseFileNameOrStdIn))]
        Stream inputBinary,

        [Option("endianness", 'e')]
        Endianness endianness,

        [Option("offset")]
        [ValidateWith(nameof(MultiBaseInt.IsPositive))]
        MultiBaseInt byteOffset
    ) {
        var bytes = ReadBinStream(inputBinary, endianness, byteOffset);
        Disassembler.PrintDisassembly(bytes);
    }

    /// <summary>
    /// Emulates running a RISC-V binary on an RV-32 processor
    /// </summary>
    /// <param name="inputBinary"></param>
    /// <param name="endianness">Original endianness of the input file</param>
    /// <param name="inputOffset">
    ///     Offset, in bytes, at which the instructions actually start in the input file
    /// </param>
    /// <param name="textOffset">
    ///     Offset, in bytes, at which to place the instructions in memory
    /// </param>
    /// <param name="memorySize">
    ///     Total size of the RAM; by default, this will be the inputBinary.Length+textOffset
    /// </param>
    /// <param name="registersToWatch"></param>
    /// <param name="memoryWatchRange"></param>
    /// <param name="shouldPrintStatus"></param>
    /// <param name="shouldSingleStep"></param>
    [Command("run", ShortDesc = "Emulate running a RISC-V binary")]
    public static void Run(
        [ParseWith(nameof(ParseFileNameOrStdIn))]
        Stream inputBinary,

        [Option("endianness", 'e')]
        Endianness endianness,

        [Option("offset")]
        [ValidateWith(nameof(MultiBaseInt.IsPositive))]
        MultiBaseInt inputOffset,

        [Option("text-offset")]
        [ValidateWith(nameof(MultiBaseInt.IsPositive))]
        MultiBaseInt textOffset,

        [Option("memory", 'm')]
        [ValidateWith(nameof(MultiBaseInt.IsPositive))]
        MultiBaseInt memorySize,

        [Option("watch-regs", 'w')]
        [ParseWith(nameof(ParseRegisterList))]
        byte[] registersToWatch,

        [Option("watch-memory-range")]
        [ParseWith(nameof(ParseRange))]
        Range memoryWatchRange,

        [Option("print-status")]
        bool? shouldPrintStatus, // nullable to be "forceable"

        [Option("single-step", 's')]
        bool shouldSingleStep = true
    ) {
        registersToWatch ??= Array.Empty<byte>();

        if (shouldSingleStep && Console.IsInputRedirected)
            throw new Exception("Can't use option '--single-step' when input is not stdin");

        // if we don't step, we probably don't want to print the status (except if we're forced to)
        shouldPrintStatus ??= shouldSingleStep;

        var bytes = ReadBinStream(inputBinary, endianness, inputOffset);

        if (memorySize == 0) {
            memorySize = bytes.Length + Math.Max(0, textOffset);
        } else {
            if (memorySize < bytes.Length)
                throw new Exception("Memory must be at least big enough to contain the instructions");
            if (memorySize > Int32.MaxValue)
                throw new Exception("Memory must be smaller than 2GiB"); // fixme: max mem_size should be 4gb
        }

        if (textOffset < 0)
            textOffset = memorySize - textOffset;

        if (textOffset > memorySize || textOffset < 0) // we assigned it earlier, it should be positive now
            throw new Exception("text-offset is outside of memory!");

        var memory = new byte[memorySize];
        bytes.CopyTo(memory.AsSpan(textOffset));

        var cpu = new Processor(memory) {
            PC = textOffset
        };

        do {
            if (shouldPrintStatus is true) {
                Console.Clear();
                cpu.PrintState();

                var memAsInts = MemoryMarshal.Cast<byte, uint>(cpu.Memory.Span)[memoryWatchRange];
                if (memAsInts.Length != 0) {
                    Console.WriteLine();
                    Console.WriteLine("Memory:");
                    for (int i = 0; i < memAsInts.Length; i++) {
                        Console.WriteLine("0x" + Convert.ToString(memAsInts[i], 16).PadLeft(16, '0'));
                    }
                }
            }

            if (registersToWatch.Length != 0) {
                Console.WriteLine();
                Console.WriteLine("Watch:");
                foreach (var regIdx in registersToWatch) {
                    var regStr = ("x" + regIdx).PadLeft(3);
                    Console.WriteLine(
                          "  "
                        + regStr
                        + " = 0x"
                        + Convert.ToString(cpu.Registers[regIdx], 16).PadLeft(16, '0')
                    );
                }
            }

            if (shouldSingleStep)
                Console.ReadKey();
        } while (cpu.Step());
    }
}