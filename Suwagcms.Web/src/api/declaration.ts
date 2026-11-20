import type { components } from "./openapi";
import { request } from "./client";

export type DeclarationStatus = components["schemas"]["DeclarationStatus"];

export function fetchDeclaration(bot: string, signal: AbortSignal, ask = true): Promise<DeclarationStatus> {
    return request<DeclarationStatus>(`/bots/${encodeURIComponent(bot)}/declaration${ask ? "" : "?ask=false"}`, { signal });
}
