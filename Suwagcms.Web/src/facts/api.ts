// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Fact = components["schemas"]["Fact"];
export type SaveFact = components["schemas"]["SaveFact"];

function factsPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/facts`;
}

export function fetchFact(bot: string, name: string, signal: AbortSignal): Promise<Fact> {
    return request<Fact>(`${factsPath(bot)}/${encodeURIComponent(name)}`, { signal });
}

export function fetchFacts(bot: string, signal: AbortSignal): Promise<Fact[]> {
    return request<Fact[]>(factsPath(bot), { signal });
}

/** Saves a fact. `ifRevision` 0 means "add: the name must be free"; a number means "I edited this revision". */
export function saveFact(bot: string, name: string, body: SaveFact, ifRevision: number): Promise<Fact> {
    return request<Fact>(`${factsPath(bot)}/${encodeURIComponent(name)}?ifRevision=${ifRevision}`, {
        method: "PUT",
        body: JSON.stringify(body),
    });
}

export function deleteFact(bot: string, name: string, ifRevision: number): Promise<undefined> {
    return request<undefined>(`${factsPath(bot)}/${encodeURIComponent(name)}?ifRevision=${ifRevision}`, { method: "DELETE" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function revisionOf(fact: Fact): number {
    return Number(fact.revision);
}
