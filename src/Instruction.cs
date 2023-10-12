namespace Tokei;

public abstract record Instruction(InstrCode Code) {
    public static readonly Instruction NOP = new Immediate(InstrCode.addi, 0, 0, 0);

    public sealed record Error(InstrCode Code, uint RawInstruction) : Instruction(Code);

    public override string ToString() => Disassembler.FormatInstruction(this);

    public sealed record Register(InstrCode Code, byte Rd, byte Rs1, byte Rs2) : Instruction(Code);

    public sealed record Immediate(InstrCode Code, byte Rd, byte Rs, int Operand) : Instruction(Code);

    // public sealed record ShortImmediate(InstrCode Instr, byte Rs, byte ShortOperand) : Instruction(Instr);

    public sealed record Store(InstrCode Code, byte Rbase, byte Rs, int Offset) : Instruction(Code);

    public sealed record Branch(InstrCode Code, byte Rs1, byte Rs2, int Offset) : JumpLike(Code, Offset);

    public sealed record UpperImmediate(InstrCode Code, byte Rd, int Operand) : Instruction(Code);

    public sealed record Jump(InstrCode Code, byte Rd, int Offset) : JumpLike(Code, Offset);

    public abstract record JumpLike(InstrCode Code, int Offset) : Instruction(Code);
}