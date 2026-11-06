namespace Suwagcms.Sdk.Format.Procedures;

public sealed record StallInstruction
{
    public string Instruction { get; init; } = "";

    public InstructionContext? Context { get; init; }
}
