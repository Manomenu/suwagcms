// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "../api/client";
import type { components } from "../api/openapi";

export type Bot = components["schemas"]["Bot"];

export function fetchBots(signal: AbortSignal): Promise<Bot[]> {
    return request<Bot[]>("/bots", { signal });
}

export function registerBot(bot: components["schemas"]["RegisterBot"]): Promise<Bot> {
    return request<Bot>("/bots", { method: "POST", body: JSON.stringify(bot) });
}
