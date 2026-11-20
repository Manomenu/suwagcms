type ResourceCollection = "facts" | "knowledge-bases" | "articles" | "actions";

export function revisionOf(resource: { revision: number | string }): number {
    return Number(resource.revision);
}

export interface ResourceApi<View, Save> {
    collection: ResourceCollection;
    list: (bot: string, signal: AbortSignal) => Promise<View[]>;
    get: (bot: string, id: string, signal: AbortSignal) => Promise<View>;
    save: (bot: string, id: string, body: Save, ifRevision: number) => Promise<View>;
    remove: (bot: string, id: string, ifRevision: number) => Promise<undefined>;
}
