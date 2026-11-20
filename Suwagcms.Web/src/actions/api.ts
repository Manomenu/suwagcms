import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "../api/client";
import type { components } from "../api/openapi";
import type { ResourceApi } from "../api/resourceApi";

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

export const actionsApi: ResourceApi<ActionView, SaveAction> = {
    collection: "actions",
    list: (bot, signal) => dataOf(api.GET("/api/bots/{bot}/actions", { params: { path: { bot } }, signal })),
    get: (bot, id, signal) => dataOf(api.GET("/api/bots/{bot}/actions/{id}", { params: { path: { bot, id } }, signal })),
    save: (bot, id, body, ifRevision) =>
        dataOf(api.PUT("/api/bots/{bot}/actions/{id}", { params: { path: { bot, id }, query: { ifRevision } }, body })),
    remove: (bot, id, ifRevision) =>
        dataOf(api.DELETE("/api/bots/{bot}/actions/{id}", { params: { path: { bot, id }, query: { ifRevision } } })),
};

export function runAction(bot: string, id: string, values: Record<string, string>): Promise<ActionTestAnswer> {
    return dataOf(api.POST("/api/bots/{bot}/actions/{id}/test", { params: { path: { bot, id } }, body: { values } }));
}
