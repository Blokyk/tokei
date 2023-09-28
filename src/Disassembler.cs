using System.Runtime.InteropServices;

namespace Tokei;

// note: for now, we could just put PrintDisassembly
//       in Decoder as an internal function, but ideally
//       this should also create and use labels for the
//       jump/branch offsets, which can become a bit complex
public static class Disassembler
{
    public static void PrintDisassembly(ReadOnlySpan<byte> bytes) {
        if (bytes.Length == 0)
            return;

        var instrs = Decode(bytes);

        // we have to do it before because there might be backwards label
        var (jmpToTargets, targetToLabel) = ExtractJumpInfo(instrs);

        var addressPadding
            = (int)Math.Ceiling(Math.Log(bytes.Length, 16*2)); // it's prettier if it's padded every 2 digits :)

        for (int i = 0; i < instrs.Length; i++) {
            if (targetToLabel.TryGetValue(i, out var label)) {
                Console.Write(new string(' ', 2 + addressPadding + 1 + 4));
                Console.Write(label);
                Console.Write(':');
                Console.WriteLine();
            }

            var instr = instrs[i];

            string instrStr;

            var isAbsoluteJmp = jmpToTargets.TryGetValue(i, out var target);

            // if this jump was successfully resolved, we replace the offset with
            // the label; otherwise, we warn about the unresolved jump target
            if (isAbsoluteJmp && targetToLabel.TryGetValue(target, out label)) {
                instrStr = FormatJumpWithLabel(instr, label);
            } else {
                instrStr = FormatInstruction(instr);
                if (isAbsoluteJmp)
                    instrStr += "\t# WARNING: target outside of loaded code";
            }

            var addrString = (i * 4).ToString("x").PadLeft(addressPadding, '0');

            Console.WriteLine(
                $"0x{addrString}:        {instrStr}");
        }
    }

    record ResolvedJumpsInfo(
        Dictionary<int, int> JumpIdxToTargetAddress,
        Dictionary<int, string> AddressToLabel
    );

    private static ResolvedJumpsInfo ExtractJumpInfo(Instruction[] instrs) {
        var jmpToTargetMap = new Dictionary<int, int>();

        for (int i = 0; i < instrs.Length; i++) {
            if (instrs[i] is Instruction.Jump jmp) {
                jmpToTargetMap.Add(i, i + jmp.Offset/4);
            }
            else if (instrs[i] is Instruction.Branch branch) {
                jmpToTargetMap.Add(i, i + branch.Offset/4);
            }
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

        for (int i = 0; i < instrs.Length; i++)
            decoded[i] = Decoder.Decode(instrs[i]);

        return decoded;
    }

    private static string FormatJumpWithLabel(Instruction instr, string label)
        => instr is Instruction.Jump j ? FormatJumpWithLabel(j, label) : FormatJumpWithLabel((Instruction.Branch)instr, label);
    private static string FormatJumpWithLabel(Instruction.Jump j, string label)
        => $"{PaddedInstr(j.Instr)} {Reg(j.Rd)}, {label}";
    private static string FormatJumpWithLabel(Instruction.Branch b, string label)
        => $"{PaddedInstr(b.Instr)} {Reg(b.Rs1)}, {Reg(b.Rs2)}, {label}";

    internal static string FormatInstruction(Instruction instr) {
        switch (instr) {
            case Instruction.Register r:
                return $"{PaddedInstr(r.Instr)} {Reg(r.Rd)}, {Reg(r.Rs1)}, {Reg(r.Rs2)}";
            case Instruction.Store s:
                return $"{PaddedInstr(s.Instr)} {Reg(s.Rbase)}, {s.Offset}({Reg(s.Rs)})";
            case Instruction.Branch b:
                return $"{PaddedInstr(b.Instr)} {Reg(b.Rs1)}, {Reg(b.Rs2)}, {Hex(b.Offset)}";
            case Instruction.UpperImmediate u:
                return $"{PaddedInstr(u.Instr)} {Reg(u.Rd)}, {Hex(u.Operand >> 12)}";
            case Instruction.Jump j:
                return $"{PaddedInstr(j.Instr)} {Reg(j.Rd)}, {Hex(j.Offset)}";
            case Instruction.Immediate i:
                if (instr.Instr is (>= InstrCode.lb and <= InstrCode.lwu) or InstrCode.jalr)
                    return $"{PaddedInstr(i.Instr)} {Reg(i.Rd)}, {i.Operand}({Reg(i.Rs)})";

                if (instr.Instr is InstrCode.ecall or InstrCode.ebreak)
                    return $"{instr.Instr}";

                return $"{PaddedInstr(i.Instr)} {Reg(i.Rd)}, {Reg(i.Rs)}, {Hex(i.Operand)}";
        }

        throw new ArgumentException("Unknown instruction type: " + instr.GetType());
    }


    static string PaddedInstr(InstrCode code) => code.ToString().PadRight(6);

    static string Hex(int i) =>
        i >= 0
            ?  "0x" + Convert.ToString(i, 16)
            : "-0x" + Convert.ToString(-i, 16);

    static string Reg(byte reg)
        => ("x" + reg).PadLeft(3);
}