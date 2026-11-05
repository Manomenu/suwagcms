// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type ActionView = components["schemas"]["ActionView"];
export type SaveAction = components["schemas"]["SaveAction"];
export type ActionField = components["schemas"]["ActionField"];

/** The generated type cannot see through the format's converter: an implementation is the word `code`, or an HTTP call. */
interface HttpCall {
    method: string;
    url: string;
    headers?: Record<string, string> | null;
    timeoutSeconds?: number | null;
}

export type Implementation = "code" | { http?: HttpCall | null } | null;

export function implementationOf(action: ActionView): Implementation {
    return action.implementation as Implementation;
}

export function isCode(action: ActionView): boolean {
    return implementationOf(action) === "code";
}

function actionsPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/actions`;
}

export function fetchActions(bot: string, signal: AbortSignal): Promise<ActionView[]> {
    return request<ActionView[]>(actionsPath(bot), { signal });
}

export function fetchAction(bot: string, id: string, signal: AbortSignal): Promise<ActionView> {
    return request<ActionView>(`${actionsPath(bot)}/${encodeURIComponent(id)}`, { signal });
}

/** Saves an action. `ifRevision` 0 means "add: the id must be free"; a number means "I edited this revision". */
export function saveAction(bot: string, id: string, body: SaveAction, ifRevision: number): Promise<ActionView> {
    return request<ActionView>(`${actionsPath(bot)}/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, {
        method: "PUT",
        body: JSON.stringify(body),
    });
}

export function deleteAction(bot: string, id: string, ifRevision: number): Promise<undefined> {
    return request<undefined>(`${actionsPath(bot)}/${encodeURIComponent(id)}?ifRevision=${ifRevision}`, { method: "DELETE" });
}

/** The generated type allows a string for an int32; the server sends a number. */
export function revisionOf(action: ActionView): number {
    return Number(action.revision);
}
