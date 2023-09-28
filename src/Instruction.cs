namespace Tokei;

public enum InstrCode {
    // Register
    add, sub, sll, slt, sltu, xor, srl, sra, or, and,
    addw, subw, sllw, srlw, sraw,

    // Immediate
    lb, lh, lw, ld, lbu, lhu, lwu,
    fence, fence_i,
    addi, slti, sltiu, xori, ori, andi, addiw,
    jalr,
    ecall, ebreak,
    CSRRW, CSRRS, CSRRC, CSRRWI, CSRRSI, CSRRCI,

    // Short immediate
    slli, srli, srai, slliw, srliw, sraiw,

    // Store
    sb, sh, sw, sd,

    // Branch
    beq, bne, blt, bge, bltu, bgeu,

    // Upper
    auipc, lui,

    // Jump
    jal,
}

public abstract record Instruction(InstrCode Instr) {
    public override string ToString() => Disassembler.FormatInstruction(this);

    public sealed record Register(InstrCode Instr, byte Rd, byte Rs1, byte Rs2) : Instruction(Instr);

    public sealed record Immediate(InstrCode Instr, byte Rd, byte Rs, int Operand) : Instruction(Instr);

    // public sealed record ShortImmediate(InstrCode Instr, byte Rs, byte ShortOperand) : Instruction(Instr);

    public sealed record Store(InstrCode Instr, byte Rbase, byte Rs, int Offset) : Instruction(Instr);

    public sealed record Branch(InstrCode Instr, byte Rs1, byte Rs2, int Offset) : Instruction(Instr);

    public sealed record UpperImmediate(InstrCode Instr, byte Rd, int Operand) : Instruction(Instr);

    public sealed record Jump(InstrCode Instr, byte Rd, int Offset) : Instruction(Instr);
}