import { request } from "./client";

export type ResourceCollection = "facts" | "knowledge-bases" | "articles" | "actions";

export function revisionOf(resource: { revision: number | string }): number {
    return Number(resource.revision);
}

export interface ResourceApi<View, Save> {
    collection: ResourceCollection;
    list: (bot: string, signal: AbortSignal, query?: URLSearchParams) => Promise<View[]>;
    get: (bot: string, id: string, signal: AbortSignal) => Promise<View>;
    save: (bot: string, id: string, body: Save, ifRevision: number) => Promise<View>;
    remove: (bot: string, id: string, ifRevision: number) => Promise<undefined>;
}

export function resourceApi<View, Save>(collection: ResourceCollection): ResourceApi<View, Save> {
    const path = (bot: string) => `/bots/${encodeURIComponent(bot)}/${collection}`;
    const item = (bot: string, id: string) => `${path(bot)}/${encodeURIComponent(id)}`;
    return {
        collection,
        list: (bot, signal, query) =>
            request<View[]>(query === undefined || query.size === 0 ? path(bot) : `${path(bot)}?${query}`, { signal }),
        get: (bot, id, signal) => request<View>(item(bot, id), { signal }),
        save: (bot, id, body, ifRevision) =>
            request<View>(`${item(bot, id)}?ifRevision=${ifRevision}`, { method: "PUT", body: JSON.stringify(body) }),
        remove: (bot, id, ifRevision) => request<undefined>(`${item(bot, id)}?ifRevision=${ifRevision}`, { method: "DELETE" }),
    };
}
