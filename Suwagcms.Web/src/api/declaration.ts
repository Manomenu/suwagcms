// Why: the profile asks the bot and the editor reads the last answer, and one feature cannot import another.
import type { components } from "./openapi";
import { request } from "./client";

export type DeclarationStatus = components["schemas"]["DeclarationStatus"];

/** Asks the bot, unless told only to read what the CMS holds. */
export function fetchDeclaration(bot: string, signal: AbortSignal, ask = true): Promise<DeclarationStatus> {
    return request<DeclarationStatus>(`/bots/${encodeURIComponent(bot)}/declaration${ask ? "" : "?ask=false"}`, { signal });
}
