using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.KnowledgeBases;

internal static class KnowledgeBaseMapping
{
    public static KnowledgeBase ToRecord(string id, SaveKnowledgeBase body) =>
        new()
        {
            Id = id,
            Title = body.Title ?? "",
            Language = body.Language ?? KnowledgeBaseLanguages.English,
        };

    public static KnowledgeBaseView Show(StoredResource stored) => Show((KnowledgeBase)stored.Record, stored.Revision);

    public static KnowledgeBaseView Show(KnowledgeBase knowledgeBase, int revision) =>
        new(knowledgeBase.Id, knowledgeBase.Title, knowledgeBase.Language, revision);
}
