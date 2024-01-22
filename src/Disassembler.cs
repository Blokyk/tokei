using System.Runtime.InteropServices;

namespace Tokei;

// note: for now, we could just put PrintDisassembly
//       in Decoder as an internal function, but ideally
//       this should also create and use labels for the
//       jump/branch offsets, which can become a bit complex
public static class Disassembler
{
    public static void PrintDisassembly(ReadOnlySpan<byte> bytes)
        => PrintDisassembly(bytes, 0, bytes.Length/4, -1);
    public static void PrintDisassembly(ReadOnlySpan<byte> bytes, int startInstr, int length, int highlightedInstr) {
        if (bytes.Length == 0)
            return;
        PrintDisassembly(Decode(bytes), startInstr, length, highlightedInstr);
    }

    public static void PrintDisassembly(Span<Instruction> instrs)
        => PrintDisassembly(instrs, 0, instrs.Length, -1);
    public static void PrintDisassembly(Span<Instruction> instrs, int startInstr, int length, int highlightedInstr) {
        if (length == 0)
            return;

        if (startInstr >= instrs.Length)
            throw new ArgumentOutOfRangeException(nameof(startInstr));
        if (startInstr + length > instrs.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        // we have to do it before because there might be backwards label
        var (jmpToTargets, targetToLabel) = ExtractJumpInfo(instrs);

        var addressPadding
            = (int)Math.Ceiling(Math.Log(instrs.Length*4, 16/2)); // it's prettier if it's padded every 2 digits :)

        for (int i = startInstr; i < startInstr + length; i++) {
            if (targetToLabel.TryGetValue(i, out var label)) {
                Console.Write(new string(' ', 2 + addressPadding + 1 + 4));
                Console.Write(label);
                Console.Write(':');
                Console.WriteLine();
            }

            if (highlightedInstr == i)
                Console.Write("--> \x1b[1m");
            else if (highlightedInstr != -1)
                Console.Write("    ");

            var instr = instrs[i];

            string instrStr;

            var isAbsoluteJmp = jmpToTargets.TryGetValue(i, out var target);

            // if this jump was successfully resolved, we replace the offset with
            // the label; otherwise, we warn about the unresolved jump target
            if (isAbsoluteJmp && targetToLabel.TryGetValue(target, out label)) {
                instrStr = FormatJumpWithLabel((Instruction.JumpLike)instr, label);
            } else {
                instrStr = FormatInstruction(instr);
                if (isAbsoluteJmp)
                    instrStr += "\t# WARNING: target outside of loaded code";
            }

            var addrString = (i * 4).ToString("x").PadLeft(addressPadding, '0');

            Console.WriteLine(
                $"0x{addrString}:        {instrStr}\x1b[0m");
        }
    }

    record ResolvedJumpsInfo(
        Dictionary<int, int> JumpIdxToTargetAddress,
        Dictionary<int, string> AddressToLabel
    );

    private static ResolvedJumpsInfo ExtractJumpInfo(Span<Instruction> instrs) {
        var jmpToTargetMap = new Dictionary<int, int>();

        for (int i = 0; i < instrs.Length; i++) {
            if (instrs[i] is Instruction.JumpLike { Offset: var offset })
                jmpToTargetMap.Add(i, i + (offset / 4));
        }

        var uniqueTargets = jmpToTargetMap.Select(kv => kv.Value).ToHashSet();

        var labelPadding
            = (int)Math.Ceiling(Math.Log(uniqueTargets.Count, 10));

        var addrToLabelMap = new Dictionary<int, string>(jmpToTargetMap.Count);

        int j = 0;
        foreach (var address in uniqueTargets) {
            if (address < 0 || address >= instrs.Length)
                continue;
            var label = "L_" + j.ToString().PadLeft(labelPadding, '0');
            addrToLabelMap.Add(address, label);
            j++;
        }

        return new(jmpToTargetMap, addrToLabelMap);
    }

    private static Instruction[] Decode(ReadOnlySpan<byte> bytes) {
        var instrs = MemoryMarshal.Cast<byte, uint>(bytes);

        var decoded = new Instruction[instrs.Length];

        for (int i = 0; i < instrs.Length; i++) {
            decoded[i] = Decoder.Decode(instrs[i]);
        }

        return decoded;
    }

    public static string FormatJumpWithLabel(Instruction.JumpLike instr, string label)
        => instr is Instruction.Jump j
            ? FormatJumpWithLabel(j, label)
            : FormatJumpWithLabel((Instruction.Branch)instr, label);
    private static string FormatJumpWithLabel(Instruction.Jump j, string label)
        => $"{PaddedInstr(j.Code)} {Reg(j.Rd)}, {label}";
    private static string FormatJumpWithLabel(Instruction.Branch b, string label)
        => $"{PaddedInstr(b.Code)} {Reg(b.Rs1)}, {Reg(b.Rs2)}, {label}";

    internal static string FormatInstruction(Instruction instr) {
        if (instr == Instruction.NOP)
            return "nop";

        switch (instr) {
            case Instruction.Register r:
                return $"{PaddedInstr(r.Code)} {Reg(r.Rd)}, {Reg(r.Rs1)}, {Reg(r.Rs2)}";
            case Instruction.Store s:
                return $"{PaddedInstr(s.Code)} {Reg(s.Rs)}, {s.Offset}(x{s.Rbase})";
            case Instruction.Branch b:
                return $"{PaddedInstr(b.Code)} {Reg(b.Rs1)}, {Reg(b.Rs2)}, {Hex(b.Offset)}";
            case Instruction.UpperImmediate u:
                return $"{PaddedInstr(u.Code)} {Reg(u.Rd)}, {Hex(u.Operand >> 12)}";
            case Instruction.Jump j:
                return $"{PaddedInstr(j.Code)} {Reg(j.Rd)}, {Hex(j.Offset)}";
            case Instruction.Immediate i:
                if (instr.Code is (>= InstrCode.lb and <= InstrCode.lwu) or InstrCode.jalr)
                    return $"{PaddedInstr(i.Code)} {Reg(i.Rd)}, {i.Operand}(x{i.Rs})"; // we don't want to pad the register part here

                if (instr.Code is InstrCode.ecall or InstrCode.ebreak)
                    return $"{instr.Code}";

                return $"{PaddedInstr(i.Code)} {Reg(i.Rd)}, {Reg(i.Rs)}, {Hex(i.Operand)}";
            case Instruction.Error e:
                var data = e.RawInstruction;
                var b0 = (byte)data;
                var b1 = (byte)(data >> 8);
                var b2 = (byte)(data >> 16);
                var b3 = (byte)(data >> 24);
                return $"<{Hex(b0)} {Hex(b1)} {Hex(b2)} {Hex(b3)}>";
        }

        throw new ArgumentException("Unknown instruction type: " + instr.GetType());
    }

    internal static string FormatInvalidInstruction(uint rawInstr) {
        var opcode = Convert.ToString(Decoder.GetOpcode(rawInstr), toBase: 2).PadLeft(7, '0');
        var rd = Convert.ToString(Decoder.GetRd(rawInstr), toBase: 2).PadLeft(5, '0');
        var f3 = Convert.ToString(Decoder.GetFunct3(rawInstr), toBase: 2).PadLeft(3, '0');
        var rs1 = Convert.ToString(Decoder.GetRs1(rawInstr), toBase: 2).PadLeft(5, '0');
        var rs2 = Convert.ToString(Decoder.GetRs2(rawInstr), toBase: 2).PadLeft(5, '0');
        var f7 = Convert.ToString(Decoder.GetFunct7(rawInstr), toBase: 2).PadLeft(7, '0');
        return $"'{f7}|{rs2}|{rs1}|{f3}|{rd}|{opcode}' (0x{Convert.ToString(rawInstr, 16).PadLeft(8, '0')})";
    }

    static string PaddedInstr(InstrCode code) => code.ToString().PadRight(6);

    static string Hex(int i) =>
        i >= 0
            ?  "0x" + Convert.ToString(i, 16)
            : "-0x" + Convert.ToString(-i, 16) + " # (0x" + Convert.ToString((uint)i, 16) + ")";

    static string Reg(byte reg)
        => ("x" + reg).PadLeft(3);
}