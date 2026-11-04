// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type KnowledgeBase = components["schemas"]["KnowledgeBaseView"];
export type SaveKnowledgeBase = components["schemas"]["SaveKnowledgeBase"];

function basesPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/knowledge-bases`;
}

export function fetchKnowledgeBases(bot: string, signal: AbortSignal): Promise<KnowledgeBase[]> {
    return request<KnowledgeBase[]>(basesPath(bot), { signal });
}

/** Saves a base. `ifRevision` 0 means "add: the id must be free"; a number means "I edited this revision". */
export function saveKnowledgeBase(bot: string, id: string, body: SaveKnowledgeBase, ifRevision: number): Promise<KnowledgeBase> {
    return request<KnowledgeBase>(`${basesPath(bot)}/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, {
        method: "PUT",
        body: JSON.stringify(body),
    });
}

export function deleteKnowledgeBase(bot: string, id: string, ifRevision: number): Promise<undefined> {
    return request<undefined>(`${basesPath(bot)}/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, { method: "DELETE" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function revisionOf(base: KnowledgeBase): number {
    return Number(base.revision);
}
