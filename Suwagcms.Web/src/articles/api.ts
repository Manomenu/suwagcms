// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type Article = components["schemas"]["ArticleView"];
export type SaveArticle = components["schemas"]["SaveArticle"];
export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];

type ProcedureList = components["schemas"]["ProcedureList"];

export interface Filters {
    knowledgeBase: string;
    language: string;
    text: string;
}

export const articlesApi = resourceApi<Article, SaveArticle>("articles");

export function fetchProcedures(bot: string, signal: AbortSignal): Promise<ProcedureList> {
    return request<ProcedureList>(`/bots/${encodeURIComponent(bot)}/procedures`, { signal });
}

export function fetchArticles(bot: string, filters: Filters, signal: AbortSignal): Promise<Article[]> {
    const query = new URLSearchParams();
    if (filters.knowledgeBase !== "") query.set("knowledgeBase", filters.knowledgeBase);
    if (filters.language !== "") query.set("language", filters.language);
    if (filters.text.trim() !== "") query.set("text", filters.text);
    return articlesApi.list(bot, signal, query);
}

/** The bases an article can be put in. */
export function fetchKnowledgeBases(bot: string, signal: AbortSignal): Promise<KnowledgeBase[]> {
    return resourceApi<KnowledgeBase, never>("knowledge-bases").list(bot, signal);
}
