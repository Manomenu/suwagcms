// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type ActionView = components["schemas"]["ActionView"];
export type SaveAction = components["schemas"]["SaveAction"];
export type ActionField = components["schemas"]["ActionField"];
export type ActionTestAnswer = components["schemas"]["ActionTestAnswer"];

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

export const actionsApi = resourceApi<ActionView, SaveAction>("actions");

/** A test run: the bot really calls the service, so nothing here is safe to repeat by itself. */
export function runAction(bot: string, id: string, values: Record<string, string>): Promise<ActionTestAnswer> {
    return request<ActionTestAnswer>(`/bots/${encodeURIComponent(bot)}/actions/${encodeURIComponent(id)}/test`, {
        method: "POST",
        body: JSON.stringify({ values }),
    });
}
