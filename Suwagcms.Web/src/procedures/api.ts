// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type ProcedureList = components["schemas"]["ProcedureList"];
export type ProcedureSummary = components["schemas"]["ProcedureSummary"];
export type ProcedureDetail = components["schemas"]["ProcedureDetail"];
export type SaveProcedure = components["schemas"]["SaveProcedure"];
export type Fact = components["schemas"]["Fact"];
export type ProcedureLayout = components["schemas"]["ProcedureLayout"];
type ArticleView = components["schemas"]["ArticleView"];

function proceduresPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/procedures`;
}

function procedurePath(bot: string, id: string): string {
    return `${proceduresPath(bot)}/${encodeURIComponent(id)}`;
}

export function fetchProcedures(bot: string, signal: AbortSignal): Promise<ProcedureList> {
    return request<ProcedureList>(proceduresPath(bot), { signal });
}

export function fetchProcedure(bot: string, id: string, signal: AbortSignal): Promise<ProcedureDetail> {
    return request<ProcedureDetail>(procedurePath(bot, id), { signal });
}

/** The facts the forms offer. The creator never decides what a fact may be used for beyond the lists it filters here. */
export function fetchFacts(bot: string, signal: AbortSignal): Promise<Fact[]> {
    return request<Fact[]>(`/bots/${encodeURIComponent(bot)}/facts`, { signal });
}

/** The articles a block may name, of every knowledge base of the bot. */
export function fetchArticles(bot: string, signal: AbortSignal): Promise<ArticleView[]> {
    return request<ArticleView[]>(`/bots/${encodeURIComponent(bot)}/articles`, { signal });
}

export function fetchLayout(bot: string, id: string, signal: AbortSignal): Promise<ProcedureLayout> {
    return request<ProcedureLayout>(`${procedurePath(bot, id)}/layout`, { signal });
}

export function saveLayout(bot: string, id: string, layout: ProcedureLayout): Promise<ProcedureLayout> {
    return request<ProcedureLayout>(`${procedurePath(bot, id)}/layout`, { method: "PUT", body: JSON.stringify(layout) });
}

/** Saves a procedure, or with `dryRun` asks what saving it would answer and changes nothing. `ifRevision` 0 means "add". */
export function saveProcedure(
    bot: string,
    id: string,
    body: SaveProcedure,
    ifRevision: number,
    dryRun: boolean,
    signal?: AbortSignal,
): Promise<ProcedureDetail> {
    const query = `?ifRevision=${ifRevision}${dryRun ? "&dryRun=true" : ""}`;
    return request<ProcedureDetail>(`${procedurePath(bot, id)}${query}`, {
        method: "PUT",
        body: JSON.stringify(body),
        signal: signal ?? null,
    });
}

export function deleteProcedure(bot: string, id: string, ifRevision: number): Promise<undefined> {
    return request<undefined>(`${procedurePath(bot, id)}?ifRevision=${ifRevision}`, { method: "DELETE" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function mistakesOf(procedure: ProcedureSummary): number {
    return Number(procedure.notReady);
}

export function revisionOfProcedure(procedure: ProcedureDetail): number {
    return Number(procedure.revision);
}

export type ActionView = components["schemas"]["ActionView"];

export function fetchActions(bot: string, signal: AbortSignal): Promise<ActionView[]> {
    return request<ActionView[]>(`/bots/${encodeURIComponent(bot)}/actions`, { signal });
}
