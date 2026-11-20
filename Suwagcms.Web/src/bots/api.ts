import { request } from "@/api/client";
import type { Bot } from "@/api/bots";
import type { components } from "@/api/openapi";

export function registerBot(bot: components["schemas"]["RegisterBot"]): Promise<Bot> {
    return request<Bot>("/bots", { method: "POST", body: JSON.stringify(bot) });
}
