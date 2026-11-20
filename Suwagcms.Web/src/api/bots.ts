import type { components } from "./openapi";
import { request } from "./client";

export type Bot = components["schemas"]["Bot"];

export function fetchBots(signal: AbortSignal): Promise<Bot[]> {
    return request<Bot[]>("/bots", { signal });
}
