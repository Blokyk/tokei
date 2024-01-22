namespace Tokei;

public static partial class Assembler
{
    private static Instruction[] Parse(Token[] tokenArray) {
        var tokens = new Stack<Token>(tokenArray.Reverse());

        var instrs = new List<Instruction>();
        var fixupInfo = new List<int>();
        var labelPositions = new Dictionary<string, int>();

        while (tokens.Count != 0) {
            var token = tokens.Pop();

            if (token == Token.Newline)
                continue;

            if (token is not Token.Identifier ident)
                throw new Exception($"Expected either a label or an instruction, but got '{token}'");

            // if the next token is a ':', then this is a label declaration
            if (tokens.TryPeek(out var nextToken) && nextToken is Token.Delimiter { Char: ':' }) {
                _ = tokens.Pop();
                // Console.WriteLine($"Found label '{ident.Text}' at offset {instrs.Count}");
                labelPositions.Add(ident.Text, instrs.Count); // not -1 because it's the instr AFTER the label we're referencing
            } else {
                var instr = ParseInstruction(ident, ref tokens);

                instrs.Add(instr);

                if (instr is SyntheticInstruction.InstrWithLabel)
                    fixupInfo.Add(instrs.Count - 1);

                // DO NOT MOVE ABOVE THE LABEL CHECK
                // we add an empty instruction since we need space to add the
                // lowered instructions, and array insertions are *really* expansive
                if (IsTwoBytesInstr(instr))
                    instrs.Add(SyntheticInstruction.Filler);

                if (tokens.TryPop(out token) && token != Token.Newline)
                    throw new Exception("Expected a newline after an instruction");
            }
        }

        foreach (var idx in fixupInfo) {
            var (inner, label) = (SyntheticInstruction.InstrWithLabel)instrs[idx];

            if (!labelPositions.TryGetValue(label, out var labelPos))
                throw new Exception($"Couldn't find label '{label}', used in instruction '{Disassembler.FormatInstruction(inner).Replace("0x0", label)}'");

            instrs[idx] = ReplaceLabel(inner, idx, labelPos);
        }

        for (int i = 0; i < instrs.Count; i++) {
            if (instrs[i] is not SyntheticInstruction synthInstr)
                continue;

            var (first, second) = Lower(synthInstr);
            instrs[i] = first;

            if (second is not null)
                instrs[++i] = second;
        }

        return instrs.ToArray();
    }

    private static bool IsTwoBytesInstr(Instruction instr)
        => instr.Code switch {
            InstrCode.la => true,
            InstrCode.li => true,
            _ => false
        };

    private static Instruction ReplaceLabel(
        Instruction instr,
        int instrPos,
        int labelPos
    ) {
        var offset = (labelPos - instrPos) * 4;

        return instr switch {
            Instruction.JumpLike jl => jl with { Offset = offset },
            SyntheticInstruction.BranchZero bz => bz with { Offset = offset },
            SyntheticInstruction.JumpAbs j => j with { Offset = offset },
            SyntheticInstruction.Call call => call with { Offset = offset },

            // * absolute, so labelPos, not offset
            SyntheticInstruction.LoadAddress la => la with { Address = labelPos },
            _ => throw new Exception($"Instruction '{instr.Code}' should not have a label attached, what happened?!")
        };
    }

    private static (Instruction first, Instruction? second) Lower(SyntheticInstruction instr) {
        switch (instr.Code) {
            case InstrCode.beqz:
            case InstrCode.bnez:
                var branchCode = instr.Code is InstrCode.beqz ? InstrCode.beq : InstrCode.bne;
                var bz = (SyntheticInstruction.BranchZero)instr;
                return (new Instruction.Branch(branchCode, bz.Rs, 0, bz.Offset), null);
            case InstrCode.j:
                var j = (SyntheticInstruction.JumpAbs)instr;
                return (new Instruction.Jump(InstrCode.jal, 0, j.Offset), null);
            case InstrCode.jr:
                var jr = (SyntheticInstruction.JumpReg)instr;
                return (new Instruction.Immediate(InstrCode.jalr, 0, jr.Rs, 0), null);
            case InstrCode.la: {
                var la = (SyntheticInstruction.LoadAddress)instr;
                var upper = la.Address & ~0xfff; // upper 31:12 bits
                var lower = la.Address & 0xfff;  // lower 11:0  bites
                return (
                    new Instruction.UpperImmediate(InstrCode.auipc, la.Rd, upper),
                    new Instruction.Immediate(InstrCode.addi, la.Rd, la.Rd, lower)
                );
            }
            case InstrCode.li: {
                var li = (SyntheticInstruction.LoadImm)instr;
                var upper = li.Imm & ~0xfff; // upper 31:12 bits
                var lower = li.Imm & 0xfff;  // lower 11:0  bites
                return (
                    new Instruction.UpperImmediate(InstrCode.lui, li.Rd, upper),
                    new Instruction.Immediate(InstrCode.addi, li.Rd, li.Rd, lower)
                );
            }
            case InstrCode.mv:
            case InstrCode.neg:
                var finalOpcode
                    = instr.Code is InstrCode.mv
                    ? InstrCode.add
                    : InstrCode.sub
                    ;
                var r2r = (SyntheticInstruction.RegToReg)instr;
                return (new Instruction.Register(finalOpcode, r2r.Rd, 0, r2r.Rs), null);
            case InstrCode.not:
                var not = (SyntheticInstruction.RegToReg)instr;
                return (new Instruction.Immediate(InstrCode.xori, not.Rd, not.Rs, ~0), null);
            case InstrCode.nop:
                return (new Instruction.Immediate(InstrCode.addi, 0, 0, 0), null);
            case InstrCode.ret:
                return (new Instruction.Immediate(InstrCode.jalr, 0, 1, 0), null);
            case InstrCode.seqz:
                var seqz = (SyntheticInstruction.Set)instr;
                return (new Instruction.Immediate(InstrCode.sltiu, seqz.Rd, seqz.Rs, 1), null);
            case InstrCode.snez:
                var snez = (SyntheticInstruction.Set)instr;
                return (new Instruction.Register(InstrCode.sltu, snez.Rd, 0, snez.Rs), null);
            case InstrCode.call:
                var call = (SyntheticInstruction.Call)instr;
                return (new Instruction.Jump(InstrCode.jal, 1, call.Offset), null);
            default:
                throw new InvalidOperationException($"Tried to lower terminal or unknown instruction '{instr.Code}'!");
        }
    }
}