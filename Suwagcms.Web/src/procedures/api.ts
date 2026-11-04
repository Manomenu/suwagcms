// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type ProcedureList = components["schemas"]["ProcedureList"];
export type ProcedureSummary = components["schemas"]["ProcedureSummary"];

export function fetchProcedures(bot: string, signal: AbortSignal): Promise<ProcedureList> {
    return request<ProcedureList>(`/bots/${encodeURIComponent(bot)}/procedures`, { signal });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function mistakesOf(procedure: ProcedureSummary): number {
    return Number(procedure.notReady);
}
