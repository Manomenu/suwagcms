using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.KnowledgeBases;

internal static class KnowledgeBaseMapping
{
    public static KnowledgeBase ToRecord(string id, SaveKnowledgeBase body) => new() { Id = id, Title = body.Title ?? "" };

    public static KnowledgeBaseView Show(StoredResource stored) => Show((KnowledgeBase)stored.Record, stored.Revision);

    public static KnowledgeBaseView Show(KnowledgeBase knowledgeBase, int revision) => new(knowledgeBase.Id, knowledgeBase.Title, revision);
}
