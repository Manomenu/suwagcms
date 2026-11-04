namespace Suwagcms.Sdk.Validation;

public static class MistakeCodes
{
    public const string FormatVersion = "format.version";
    public const string FormatInvalid = "format.invalid";
    public const string DuplicateId = "resource.duplicateId";
    public const string FactNameInvalid = "fact.name.invalid";
    public const string FactTypeInvalid = "fact.type.invalid";
    public const string FactDescriptionMissing = "fact.description.missing";
    public const string FactWritableByInvalid = "fact.writableBy.invalid";
    public const string FactEnumTooFewValues = "fact.enum.tooFewValues";
    public const string FactEnumValueBlank = "fact.enum.valueBlank";
    public const string FactEnumValueRepeated = "fact.enum.valueRepeated";
    public const string FactIntBoundMissing = "fact.int.boundMissing";
    public const string FactIntRangeInverted = "fact.int.rangeInverted";
    public const string FactTextMaxLengthInvalid = "fact.text.maxLengthInvalid";
    public const string FactConstraintForeign = "fact.constraint.foreign";
    public const string ProcedureIdInvalid = "procedure.id.invalid";
    public const string ProcedureDescriptionMissing = "procedure.description.missing";
    public const string StepNameInvalid = "step.name.invalid";
    public const string StepBlockUnknown = "step.block.unknown";
    public const string StepBlockMultiple = "step.block.multiple";
    public const string StepMissing = "step.missing";
    public const string ReferenceMissing = "reference.missing";
    public const string FactCollectNotWritable = "fact.collect.notWritable";
    public const string ConditionKindInvalid = "condition.kind.invalid";
    public const string ConditionTypeMisfit = "condition.type.misfit";
    public const string ConditionValueInvalid = "condition.value.invalid";
    public const string AssignValueInvalid = "assign.value.invalid";
    public const string ProcedureFirstStepMissing = "procedure.firstStep.missing";
    public const string StepBlockMissing = "step.block.missing";
    public const string StepBlockEmpty = "step.block.empty";
    public const string StepUnreachable = "step.unreachable";
    public const string NextDefaultMissing = "next.default.missing";
    public const string ProcedureNoWhenStalled = "procedure.collect.noWhenStalled";
    public const string ProcedureInstructionBlank = "procedure.whenStalled.blank";
    public const string BotConversationMissing = "bot.conversation.missing";
    public const string VersionBot = "version.bot";
    public const string VersionNumber = "version.number";
    public const string VersionDigest = "version.digest";
    public const string VersionConflict = "version.conflict";
}
