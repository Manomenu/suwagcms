// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Article = components["schemas"]["ArticleView"];
export type SaveArticle = components["schemas"]["SaveArticle"];
export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];

export interface Filters {
    knowledgeBase: string;
    language: string;
    text: string;
}

function botPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}`;
}

export function fetchArticles(bot: string, filters: Filters, signal: AbortSignal): Promise<Article[]> {
    const query = new URLSearchParams();
    if (filters.knowledgeBase !== "") query.set("knowledgeBase", filters.knowledgeBase);
    if (filters.language !== "") query.set("language", filters.language);
    if (filters.text.trim() !== "") query.set("text", filters.text);
    const suffix = query.size === 0 ? "" : `?${query.toString()}`;
    return request<Article[]>(`${botPath(bot)}/articles${suffix}`, { signal });
}

export function fetchArticle(bot: string, id: string, signal: AbortSignal): Promise<Article> {
    return request<Article>(`${botPath(bot)}/articles/${encodeURIComponent(id)}`, { signal });
}

/** The languages an article can be in; the server refuses any other. */
export const LANGUAGES = ["pl", "en"] as const;

/** The bases an article can be put in. */
export function fetchKnowledgeBases(bot: string, signal: AbortSignal): Promise<KnowledgeBase[]> {
    return request<KnowledgeBase[]>(`${botPath(bot)}/knowledge-bases`, { signal });
}

/** Saves an article. `ifRevision` 0 means "add: the id must be free"; a number means "I edited this revision". */
export function saveArticle(bot: string, id: string, body: SaveArticle, ifRevision: number): Promise<Article> {
    return request<Article>(`${botPath(bot)}/articles/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, {
        method: "PUT",
        body: JSON.stringify(body),
    });
}

export function deleteArticle(bot: string, id: string, ifRevision: number): Promise<undefined> {
    return request<undefined>(`${botPath(bot)}/articles/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, { method: "DELETE" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function revisionOf(article: Article): number {
    return Number(article.revision);
}
