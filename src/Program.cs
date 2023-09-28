using System.Runtime.InteropServices;
using Tokei;

if (args.Length != 1) {
    Console.Error.WriteLine("Usage: tokei <input.bin>");
    return 1;
}

if (args[0].StartsWith("0x")) {
    var instr = Convert.ToUInt32(args[0][2..], 16);
    Disassembler.PrintDisassembly(BitConverter.GetBytes(instr));
    Console.WriteLine(Disassembler.FormatInstruction(Decoder.Decode(instr)));
    return 0;
}

var bytes = File.ReadAllBytes(args[0]);
SpanUtils.Reverse4ByteEndianness(bytes);
Disassembler.PrintDisassembly(bytes);
return 0;