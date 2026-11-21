using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Validation;

[JsonConverter(typeof(JsonStringEnumConverter<MistakeCode>))]
public enum MistakeCode
{
    [JsonStringEnumMemberName("format.version")]
    FormatVersion,

    [JsonStringEnumMemberName("format.invalid")]
    FormatInvalid,

    [JsonStringEnumMemberName("resource.duplicateId")]
    DuplicateId,

    [JsonStringEnumMemberName("fact.name.invalid")]
    FactNameInvalid,

    [JsonStringEnumMemberName("fact.type.invalid")]
    FactTypeInvalid,

    [JsonStringEnumMemberName("fact.description.missing")]
    FactDescriptionMissing,

    [JsonStringEnumMemberName("fact.writableBy.invalid")]
    FactWritableByInvalid,

    [JsonStringEnumMemberName("fact.enum.tooFewValues")]
    FactEnumTooFewValues,

    [JsonStringEnumMemberName("fact.enum.valueBlank")]
    FactEnumValueBlank,

    [JsonStringEnumMemberName("fact.enum.valueRepeated")]
    FactEnumValueRepeated,

    [JsonStringEnumMemberName("fact.enum.valueUnusable")]
    FactEnumValueUnusable,

    [JsonStringEnumMemberName("fact.enum.valueReserved")]
    FactEnumValueOther,

    [JsonStringEnumMemberName("fact.int.boundMissing")]
    FactIntBoundMissing,

    [JsonStringEnumMemberName("fact.int.rangeInverted")]
    FactIntRangeInverted,

    [JsonStringEnumMemberName("fact.text.maxLengthInvalid")]
    FactTextMaxLengthInvalid,

    [JsonStringEnumMemberName("fact.pattern.regexMissing")]
    FactPatternRegexMissing,

    [JsonStringEnumMemberName("fact.pattern.regexInvalid")]
    FactPatternRegexInvalid,

    [JsonStringEnumMemberName("fact.pattern.regexMatchesEmpty")]
    FactPatternRegexMatchesEmpty,

    [JsonStringEnumMemberName("fact.pattern.meansMissing")]
    FactPatternMeansMissing,

    [JsonStringEnumMemberName("fact.pattern.exampleBlank")]
    FactPatternExampleBlank,

    [JsonStringEnumMemberName("fact.pattern.exampleMismatch")]
    FactPatternExampleMismatch,

    [JsonStringEnumMemberName("fact.pattern.exampleTooLong")]
    FactPatternExampleTooLong,

    [JsonStringEnumMemberName("fact.pattern.tellCustomerInvalid")]
    FactPatternTellCustomerInvalid,

    [JsonStringEnumMemberName("fact.constraint.foreign")]
    FactConstraintForeign,

    [JsonStringEnumMemberName("fact.local.nameClash")]
    FactLocalNameClash,

    [JsonStringEnumMemberName("fact.name.procedurePrefix")]
    FactNameProcedurePrefix,

    [JsonStringEnumMemberName("fact.enum.valueInUse")]
    FactEnumValueInUse,

    [JsonStringEnumMemberName("fact.type.inUse")]
    FactTypeInUse,

    [JsonStringEnumMemberName("fact.range.inUse")]
    FactRangeInUse,

    [JsonStringEnumMemberName("fact.pattern.inUse")]
    FactPatternInUse,

    [JsonStringEnumMemberName("fact.writableBy.inUse")]
    FactWritableByInUse,

    [JsonStringEnumMemberName("knowledgeBase.id.invalid")]
    KnowledgeBaseIdInvalid,

    [JsonStringEnumMemberName("knowledgeBase.title.missing")]
    KnowledgeBaseTitleMissing,

    [JsonStringEnumMemberName("knowledgeBase.title.repeated")]
    KnowledgeBaseTitleRepeated,

    [JsonStringEnumMemberName("knowledgeBase.language.unknown")]
    KnowledgeBaseLanguageUnknown,

    [JsonStringEnumMemberName("knowledgeBase.embedding.missing")]
    KnowledgeBaseEmbeddingMissing,

    [JsonStringEnumMemberName("knowledgeBase.results.outOfRange")]
    KnowledgeBaseResultsOutOfRange,

    [JsonStringEnumMemberName("article.id.invalid")]
    ArticleIdInvalid,

    [JsonStringEnumMemberName("article.knowledgeBase.unknown")]
    ArticleKnowledgeBaseUnknown,

    [JsonStringEnumMemberName("article.title.missing")]
    ArticleTitleMissing,

    [JsonStringEnumMemberName("article.content.missing")]
    ArticleContentMissing,

    [JsonStringEnumMemberName("article.llmGuide.missing")]
    ArticleLlmGuideMissing,

    [JsonStringEnumMemberName("article.description.missing")]
    ArticleDescriptionMissing,

    [JsonStringEnumMemberName("article.content.tooLong")]
    ArticleContentTooLong,

    [JsonStringEnumMemberName("article.llmGuide.tooLong")]
    ArticleLlmGuideTooLong,

    [JsonStringEnumMemberName("procedure.id.invalid")]
    ProcedureIdInvalid,

    [JsonStringEnumMemberName("procedure.description.missing")]
    ProcedureDescriptionMissing,

    [JsonStringEnumMemberName("step.name.invalid")]
    StepNameInvalid,

    [JsonStringEnumMemberName("step.block.unknown")]
    StepBlockUnknown,

    [JsonStringEnumMemberName("step.block.multiple")]
    StepBlockMultiple,

    [JsonStringEnumMemberName("step.missing")]
    StepMissing,

    [JsonStringEnumMemberName("link.procedure.notInGuide")]
    LinkProcedureNotInGuide,

    [JsonStringEnumMemberName("link.procedure.conversation")]
    LinkProcedureConversation,

    [JsonStringEnumMemberName("reference.missing")]
    ReferenceMissing,

    [JsonStringEnumMemberName("reference.malformed")]
    ReferenceMalformed,

    [JsonStringEnumMemberName("reference.loop")]
    ReferenceLoop,

    [JsonStringEnumMemberName("fact.collect.notWritable")]
    FactCollectNotWritable,

    [JsonStringEnumMemberName("start.target.conversation")]
    StartTargetConversation,

    [JsonStringEnumMemberName("condition.kind.invalid")]
    ConditionKindInvalid,

    [JsonStringEnumMemberName("condition.type.misfit")]
    ConditionTypeMisfit,

    [JsonStringEnumMemberName("condition.value.invalid")]
    ConditionValueInvalid,

    [JsonStringEnumMemberName("assign.value.invalid")]
    AssignValueInvalid,

    [JsonStringEnumMemberName("procedure.firstStep.missing")]
    ProcedureFirstStepMissing,

    [JsonStringEnumMemberName("step.block.missing")]
    StepBlockMissing,

    [JsonStringEnumMemberName("step.block.empty")]
    StepBlockEmpty,

    [JsonStringEnumMemberName("step.inform.empty")]
    StepInformEmpty,

    [JsonStringEnumMemberName("step.inform.pointBlank")]
    StepInformPointBlank,

    [JsonStringEnumMemberName("step.inform.pointName")]
    StepInformPointName,

    [JsonStringEnumMemberName("instruction.blank")]
    InstructionBlank,

    [JsonStringEnumMemberName("knowledge.base.missing")]
    KnowledgeBaseMissing,

    [JsonStringEnumMemberName("knowledge.base.repeated")]
    KnowledgeBaseRepeated,

    [JsonStringEnumMemberName("knowledge.article.otherBase")]
    KnowledgeArticleOtherBase,

    [JsonStringEnumMemberName("knowledge.entry.empty")]
    KnowledgeEntryEmpty,

    [JsonStringEnumMemberName("knowledge.query.blank")]
    KnowledgeQueryBlank,

    [JsonStringEnumMemberName("knowledge.query.noArticles")]
    KnowledgeQueryNoArticles,

    [JsonStringEnumMemberName("step.unreachable")]
    StepUnreachable,

    [JsonStringEnumMemberName("next.default.missing")]
    NextDefaultMissing,

    [JsonStringEnumMemberName("next.default.notLast")]
    NextDefaultNotLast,

    [JsonStringEnumMemberName("procedure.collect.noWhenStalled")]
    ProcedureNoWhenStalled,

    [JsonStringEnumMemberName("procedure.whenStalled.blank")]
    ProcedureInstructionBlank,

    [JsonStringEnumMemberName("procedure.whenInterrupted.invalid")]
    ProcedureWhenInterruptedInvalid,

    [JsonStringEnumMemberName("procedure.whenInterrupted.onConversation")]
    ProcedureWhenInterruptedOnConversation,

    [JsonStringEnumMemberName("bot.conversation.missing")]
    BotConversationMissing,

    [JsonStringEnumMemberName("version.bot")]
    VersionBot,

    [JsonStringEnumMemberName("version.number")]
    VersionNumber,

    [JsonStringEnumMemberName("version.digest")]
    VersionDigest,

    [JsonStringEnumMemberName("version.conflict")]
    VersionConflict,

    [JsonStringEnumMemberName("version.notHeld")]
    VersionNotHeld,

    [JsonStringEnumMemberName("availability.invalid")]
    AvailabilityInvalid,

    [JsonStringEnumMemberName("declaration.invalid")]
    DeclarationInvalid,

    [JsonStringEnumMemberName("declaration.bot")]
    DeclarationBot,

    [JsonStringEnumMemberName("declaration.formatVersion.missing")]
    DeclarationFormatMissing,

    [JsonStringEnumMemberName("declaration.blocks.missing")]
    DeclarationBlocksMissing,

    [JsonStringEnumMemberName("declaration.block.unknown")]
    DeclarationBlockUnknown,

    [JsonStringEnumMemberName("declaration.formatVersion")]
    DeclarationFormatVersion,

    [JsonStringEnumMemberName("declaration.block")]
    DeclarationBlock,

    [JsonStringEnumMemberName("declaration.knowledge")]
    DeclarationKnowledge,

    [JsonStringEnumMemberName("declaration.embeddingProfile")]
    DeclarationEmbeddingProfile,

    [JsonStringEnumMemberName("declaration.namedArticlesTooLong")]
    DeclarationNamedArticlesTooLong,

    [JsonStringEnumMemberName("action.id.invalid")]
    ActionIdInvalid,

    [JsonStringEnumMemberName("action.description.missing")]
    ActionDescriptionMissing,

    [JsonStringEnumMemberName("action.implementation.missing")]
    ActionImplementationMissing,

    [JsonStringEnumMemberName("action.http.method")]
    ActionMethodInvalid,

    [JsonStringEnumMemberName("action.http.url")]
    ActionUrlInvalid,

    [JsonStringEnumMemberName("action.http.urlCredentials")]
    ActionUrlCredentials,

    [JsonStringEnumMemberName("action.http.urlParameter")]
    ActionUrlParameter,

    [JsonStringEnumMemberName("action.http.timeout")]
    ActionTimeoutInvalid,

    [JsonStringEnumMemberName("action.http.header")]
    ActionHeaderInvalid,

    [JsonStringEnumMemberName("action.secret.name")]
    ActionSecretName,

    [JsonStringEnumMemberName("action.secret.inUrl")]
    ActionSecretInUrl,

    [JsonStringEnumMemberName("action.secret.literal")]
    ActionSecretLiteral,

    [JsonStringEnumMemberName("codeAction.id.missing")]
    CodeActionIdMissing,

    [JsonStringEnumMemberName("codeAction.description.missing")]
    CodeActionDescriptionMissing,

    [JsonStringEnumMemberName("codeAction.repeated")]
    CodeActionRepeated,

    [JsonStringEnumMemberName("codeAction.id.invalid")]
    CodeActionIdInvalid,

    [JsonStringEnumMemberName("codeAction.field.invalid")]
    CodeActionFieldInvalid,

    [JsonStringEnumMemberName("codeAction.field.repeated")]
    CodeActionFieldRepeated,

    [JsonStringEnumMemberName("action.mapping.typeMismatch")]
    ActionMappingTypeMismatch,

    [JsonStringEnumMemberName("action.mapping.tooWide")]
    ActionMappingTooWide,

    [JsonStringEnumMemberName("action.mapping.resultTwice")]
    ActionMappingResultTwice,

    [JsonStringEnumMemberName("action.code.mapping")]
    ActionCodeMapping,

    [JsonStringEnumMemberName("action.field.inUse")]
    ActionFieldInUse,

    [JsonStringEnumMemberName("step.action.fieldForeign")]
    StepActionFieldForeign,

    [JsonStringEnumMemberName("step.action.noOnFailure")]
    StepActionNoOnFailure,

    [JsonStringEnumMemberName("step.action.unmappedParameter")]
    StepActionUnmappedParameter,

    [JsonStringEnumMemberName("action.code.undeclared")]
    ActionCodeUndeclared,

    [JsonStringEnumMemberName("action.code.contract")]
    ActionCodeContract,

    [JsonStringEnumMemberName("action.code.conflict")]
    ActionCodeConflict,

    [JsonStringEnumMemberName("persona.text.tooLong")]
    PersonaTextTooLong,

    [JsonStringEnumMemberName("persona.required")]
    PersonaRequired,

    [JsonStringEnumMemberName("persona.incomplete")]
    PersonaIncomplete,

    [JsonStringEnumMemberName("declaration.persona.unknown")]
    DeclarationPersonaUnknown,

    [JsonStringEnumMemberName("bot.notFound")]
    BotNotFound,

    [JsonStringEnumMemberName("bot.idInvalid")]
    BotIdInvalid,

    [JsonStringEnumMemberName("bot.idTaken")]
    BotIdTaken,

    [JsonStringEnumMemberName("bot.nameMissing")]
    BotNameMissing,

    [JsonStringEnumMemberName("bot.addressInvalid")]
    BotAddressInvalid,

    [JsonStringEnumMemberName("resource.missing")]
    ResourceMissing,

    [JsonStringEnumMemberName("resource.exists")]
    ResourceExists,

    [JsonStringEnumMemberName("resource.stale")]
    ResourceStale,

    [JsonStringEnumMemberName("resource.sameId")]
    ResourceSameId,

    [JsonStringEnumMemberName("change.twice")]
    ChangedTwice,

    [JsonStringEnumMemberName("changes.otherBot")]
    ChangesOtherBot,

    [JsonStringEnumMemberName("save.unknownField")]
    SaveUnknownField,

    [JsonStringEnumMemberName("save.invalid")]
    SaveInvalid,

    [JsonStringEnumMemberName("action.code.readOnly")]
    ActionCodeReadOnly,

    [JsonStringEnumMemberName("action.code.declared")]
    ActionCodeDeclared,

    [JsonStringEnumMemberName("action.test.value")]
    ActionTestValueUnfit,

    [JsonStringEnumMemberName("availability.kind")]
    AvailabilityKindRefused,

    [JsonStringEnumMemberName("availability.conversation")]
    AvailabilityConversationRefused,

    [JsonStringEnumMemberName("procedure.conversation.required")]
    ProcedureConversationRequired,

    [JsonStringEnumMemberName("version.missing")]
    VersionMissing,

    [JsonStringEnumMemberName("version.unchanged")]
    VersionUnchanged,
}
