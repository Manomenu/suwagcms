import { request } from "../api/client";
import type { components } from "../api/openapi";
import { resourceApi } from "../api/resourceApi";

export type ActionView = components["schemas"]["ActionView"];
export type SaveAction = components["schemas"]["SaveAction"];
export type ActionField = components["schemas"]["ActionField"];
export type ActionTestAnswer = components["schemas"]["ActionTestAnswer"];

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

export function runAction(bot: string, id: string, values: Record<string, string>): Promise<ActionTestAnswer> {
    return request<ActionTestAnswer>(`/bots/${encodeURIComponent(bot)}/actions/${encodeURIComponent(id)}/test`, {
        method: "POST",
        body: JSON.stringify({ values }),
    });
}
