import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";

export type ProcedureList = components["schemas"]["ProcedureList"];
export type ProcedureSummary = components["schemas"]["ProcedureSummary"];
export type ProcedureDetail = components["schemas"]["ProcedureDetail"];
export type SaveProcedure = components["schemas"]["SaveProcedure"];
export type Fact = components["schemas"]["Fact"];
type ArticleView = components["schemas"]["ArticleView"];
type KnowledgeBaseView = components["schemas"]["KnowledgeBaseView"];

export function fetchProcedures(bot: string, signal: AbortSignal): Promise<ProcedureList> {
    return dataOf(api.GET("/api/bots/{bot}/procedures", { params: { path: { bot } }, signal }));
}

export function fetchProcedure(bot: string, id: string, signal: AbortSignal): Promise<ProcedureDetail> {
    return dataOf(api.GET("/api/bots/{bot}/procedures/{id}", { params: { path: { bot, id } }, signal }));
}

export function fetchFacts(bot: string, signal: AbortSignal): Promise<Fact[]> {
    return dataOf(api.GET("/api/bots/{bot}/facts", { params: { path: { bot } }, signal }));
}

export function fetchArticles(bot: string, signal: AbortSignal): Promise<ArticleView[]> {
    return dataOf(api.GET("/api/bots/{bot}/articles", { params: { path: { bot } }, signal }));
}

export function fetchKnowledgeBases(bot: string, signal: AbortSignal): Promise<KnowledgeBaseView[]> {
    return dataOf(api.GET("/api/bots/{bot}/knowledge-bases", { params: { path: { bot } }, signal }));
}

export function saveProcedure(
    bot: string,
    id: string,
    body: SaveProcedure,
    ifRevision: number,
    dryRun: boolean,
    signal?: AbortSignal,
): Promise<ProcedureDetail> {
    const query = dryRun ? { ifRevision, dryRun } : { ifRevision };
    return dataOf(api.PUT("/api/bots/{bot}/procedures/{id}", { params: { path: { bot, id }, query }, body, signal: signal ?? null }));
}

export function deleteProcedure(bot: string, id: string, ifRevision: number): Promise<undefined> {
    return dataOf(api.DELETE("/api/bots/{bot}/procedures/{id}", { params: { path: { bot, id }, query: { ifRevision } } }));
}

export function mistakesOf(procedure: ProcedureSummary): number {
    return Number(procedure.notReady);
}

export function revisionOfProcedure(procedure: ProcedureDetail): number {
    return Number(procedure.revision);
}

export type ActionView = components["schemas"]["ActionView"];

export function fetchActions(bot: string, signal: AbortSignal): Promise<ActionView[]> {
    return dataOf(api.GET("/api/bots/{bot}/actions", { params: { path: { bot } }, signal }));
}
