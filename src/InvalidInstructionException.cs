namespace Tokei;

public class InvalidInstructionException : Exception
{
    public InvalidInstructionException() : base("Tried to execute an invalid instruction!") {}
    public InvalidInstructionException(uint rawInstr, string message)
        : base(message + ": " + Disassembler.FormatInvalidInstruction(rawInstr)) {}
    public InvalidInstructionException(uint rawInstr)
        : this(rawInstr, "Tried to execute an invalid instruction") {}
}