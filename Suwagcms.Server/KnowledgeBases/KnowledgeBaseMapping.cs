using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.KnowledgeBases;

internal static class KnowledgeBaseMapping
{
    // Why: a profile or results left out is kept empty, so the validator refuses it with its fix instead of the server choosing one.
    public static KnowledgeBase ToRecord(string id, SaveKnowledgeBase body) =>
        new()
        {
            Id = id,
            Title = body.Title ?? "",
            Language = body.Language ?? KnowledgeBaseLanguages.English,
            Embedding = body.Embedding ?? "",
            Results = body.Results ?? 0,
        };

    public static KnowledgeBaseView Show(StoredResource stored) => Show((KnowledgeBase)stored.Record, stored.Revision);

    public static KnowledgeBaseView Show(KnowledgeBase knowledgeBase, int revision) =>
        new(knowledgeBase.Id, knowledgeBase.Title, knowledgeBase.Language, knowledgeBase.Embedding, knowledgeBase.Results, revision);
}
