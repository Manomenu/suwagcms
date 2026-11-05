namespace Suwagcms.Sdk.Format;

public sealed record StallInstruction
{
    public string Instruction { get; init; } = "";

    public InstructionContext? Context { get; init; }
}
