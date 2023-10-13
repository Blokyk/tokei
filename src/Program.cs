using System.Runtime.InteropServices;

using StarKid;

namespace Tokei;

[CommandGroup("tokei", ShortDesc = "A RISC-V toolchain, written in C#")]
internal static partial class TokeiApp
{
    private const string notFoundMsg = "File couldn't be found";

    /// <summary>Assemble RISC-V assembly into raw machine code</summary>
    /// <param name="srcFile">Assembly file to convert</param>
    /// <param name="outputFile">Path to the output raw binary file</param>
    [Command("asm")]
    public static void Assemble(
        [ValidateWith(nameof(FileInfo.Exists), notFoundMsg)]
        FileInfo srcFile,

        [Option("output", 'o')]
        [ValidateWith(nameof(CheckFileIsWritable))]
        FileInfo? outputFile
    ) {
        outputFile ??= new FileInfo("a.out");

        using var srcStream = new StreamReader(srcFile.OpenRead());
        var src = srcStream.ReadToEnd();

        var bytes = Assembler.Assemble(src);

        using var outputStream
            = outputFile.Exists
            ? outputFile.Open(FileMode.Truncate)
            : outputFile.Create();

        outputStream.Write(bytes);
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
        [ValidateWith(nameof(FileInfo.Exists), notFoundMsg)]
        FileInfo inputBinary,

        [Option("endianness", 'e')]
        Endianness endianness,

        [Option("offset")]
        [ValidateWith(nameof(MultiBaseInt.IsPositive))]
        MultiBaseInt byteOffset
    ) {
        var bytes = ReadBinFile(inputBinary, endianness, byteOffset);
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
        [ValidateWith(nameof(FileInfo.Exists), notFoundMsg)]
        FileInfo inputBinary,

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

        var bytes = ReadBinFile(inputBinary, endianness, inputOffset);

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

                var memAsInts = MemoryMarshal.Cast<byte, uint>(cpu.Memory)[memoryWatchRange];
                if (memAsInts.Length != 0) {
                    Console.WriteLine();
                    Console.WriteLine("Memory:");
                    for (int i = 0; i < memAsInts.Length; i++) {
                        Console.WriteLine("0x" + Convert.ToString(memAsInts[i], 16).PadLeft(8, '0'));
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
                        + Convert.ToString(cpu.Registers[regIdx], 16).PadLeft(8, '0')
                    );
                }
            }

            if (shouldSingleStep)
                Console.ReadKey();
        } while (cpu.MoveNext());
    }
}