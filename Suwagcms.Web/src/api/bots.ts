// Why: the bots screen and the shell's panel both read the bots, and one feature cannot import another.
import type { components } from "./openapi";
import { request } from "./client";

export type Bot = components["schemas"]["Bot"];

export function fetchBots(signal: AbortSignal): Promise<Bot[]> {
    return request<Bot[]>("/bots", { signal });
}
