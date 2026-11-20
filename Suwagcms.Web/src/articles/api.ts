import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";
import type { ResourceApi } from "../api/resourceApi";

export type Article = components["schemas"]["ArticleView"];
export type SaveArticle = components["schemas"]["SaveArticle"];
export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];

export interface Filters {
    knowledgeBase: string;
    language: string;
    text: string;
}

export const articlesApi: ResourceApi<Article, SaveArticle> = {
    collection: "articles",
    list: (bot, signal) => dataOf(api.GET("/api/bots/{bot}/articles", { params: { path: { bot } }, signal })),
    get: (bot, id, signal) => dataOf(api.GET("/api/bots/{bot}/articles/{id}", { params: { path: { bot, id } }, signal })),
    save: (bot, id, body, ifRevision) =>
        dataOf(api.PUT("/api/bots/{bot}/articles/{id}", { params: { path: { bot, id }, query: { ifRevision } }, body })),
    remove: (bot, id, ifRevision) =>
        dataOf(api.DELETE("/api/bots/{bot}/articles/{id}", { params: { path: { bot, id }, query: { ifRevision } } })),
};

export function fetchProcedures(bot: string, signal: AbortSignal) {
    return dataOf(api.GET("/api/bots/{bot}/procedures", { params: { path: { bot } }, signal }));
}

export function fetchArticles(bot: string, filters: Filters, signal: AbortSignal): Promise<Article[]> {
    const query = {
        ...(filters.knowledgeBase === "" ? {} : { knowledgeBase: filters.knowledgeBase }),
        ...(filters.language === "" ? {} : { language: filters.language }),
        ...(filters.text.trim() === "" ? {} : { text: filters.text }),
    };
    return dataOf(api.GET("/api/bots/{bot}/articles", { params: { path: { bot }, query }, signal }));
}

export function fetchKnowledgeBases(bot: string, signal: AbortSignal): Promise<KnowledgeBase[]> {
    return dataOf(api.GET("/api/bots/{bot}/knowledge-bases", { params: { path: { bot } }, signal }));
}
