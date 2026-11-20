import type { components } from "./openapi";
import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "./client";

export type DeclarationStatus = components["schemas"]["DeclarationStatus"];

export function fetchDeclaration(bot: string, signal: AbortSignal, ask = true): Promise<DeclarationStatus> {
    return dataOf(api.GET("/api/bots/{bot}/declaration", { params: { path: { bot }, query: ask ? {} : { ask: false } }, signal }));
}
