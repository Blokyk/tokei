namespace Tokei;

public enum InstrCode {
    beqz = -1, bnez = -2,
    j = -3, jr = -4,
    la = -5, li = -6, mv = -7,
    neg = -8, not = -9,
    nop = -10,
    ret = -11,
    seqz = -12, snez = -13,

    ERROR = 0,

    // Register
    add, sub, sll, slt, sltu, xor, srl, sra, or, and,
    // addw, subw, sllw, srlw, sraw,

    // Immediate
    lb, lh, lw, ld, lbu, lhu, lwu,
    fence, fence_i,
    addi, slti, sltiu, xori, ori, andi, //addiw,
    jalr,
    ecall, ebreak,
    CSRRW, CSRRS, CSRRC, CSRRWI, CSRRSI, CSRRCI,

    // Short immediate
    slli, srli, srai, //slliw, srliw, sraiw,

    // Store
    sb, sh, sw, sd,

    // Branch
    beq, bne, blt, bge, bltu, bgeu,

    // Upper
    auipc, lui,

    // Jump
    jal,

}

internal static class InstrCodeUtils
{
    public static bool TryParse(ReadOnlySpan<char> str, out InstrCode code) {
        code = str switch {
            "add"     => InstrCode.add,
            "addi"    => InstrCode.addi,
            "and"     => InstrCode.and,
            "andi"    => InstrCode.andi,
            "auipc"   => InstrCode.auipc,
            "beq"     => InstrCode.beq,
            "bge"     => InstrCode.bge,
            "bgeu"    => InstrCode.bgeu,
            "blt"     => InstrCode.blt,
            "bltu"    => InstrCode.bltu,
            "bne"     => InstrCode.bne,
            "ebreak"  => InstrCode.ebreak,
            "ecall"   => InstrCode.ecall,
            "fence"   => InstrCode.fence,
            "fence.i" => InstrCode.fence_i,
            "jal"     => InstrCode.jal,
            "jalr"    => InstrCode.jalr,
            "lb"      => InstrCode.lb,
            "lbu"     => InstrCode.lbu,
            "ld"      => InstrCode.ld,
            "lh"      => InstrCode.lh,
            "lhu"     => InstrCode.lhu,
            "lui"     => InstrCode.lui,
            "lw"      => InstrCode.lw,
            "lwu"     => InstrCode.lwu,
            "or"      => InstrCode.or,
            "ori"     => InstrCode.ori,
            "sb"      => InstrCode.sb,
            "sd"      => InstrCode.sd,
            "sh"      => InstrCode.sh,
            "sll"     => InstrCode.sll,
            "slli"    => InstrCode.slli,
            "slt"     => InstrCode.slt,
            "slti"    => InstrCode.slti,
            "sltiu"   => InstrCode.sltiu,
            "sltu"    => InstrCode.sltu,
            "sra"     => InstrCode.sra,
            "srai"    => InstrCode.srai,
            "srl"     => InstrCode.srl,
            "srli"    => InstrCode.srli,
            "sub"     => InstrCode.sub,
            "sw"      => InstrCode.sw,
            "xor"     => InstrCode.xor,
            "xori"    => InstrCode.xori,

            // pseudo-instructions
            "beqz" => InstrCode.beqz,
            "bnez" => InstrCode.bnez,
            "j" => InstrCode.j,
            "jr" => InstrCode.jr,
            "la" => InstrCode.la,
            "li" => InstrCode.li,
            "mv" => InstrCode.mv,
            "neg" => InstrCode.neg,
            "nop" => InstrCode.nop,
            "not" => InstrCode.not,
            "ret" => InstrCode.ret,
            "seqz" => InstrCode.seqz,
            "snez" => InstrCode.snez,

            _ => InstrCode.ERROR
        };

        return code != InstrCode.ERROR;
    }
}
