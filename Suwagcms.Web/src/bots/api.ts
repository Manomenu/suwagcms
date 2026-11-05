// This feature's HTTP calls, typed from the generated OpenAPI types — never a hand-written copy of a server model.
import { request } from "@/api/client";
import type { Bot } from "@/api/bots";
import type { components } from "@/api/openapi";

export function registerBot(bot: components["schemas"]["RegisterBot"]): Promise<Bot> {
    return request<Bot>("/bots", { method: "POST", body: JSON.stringify(bot) });
}
