import { dataOf } from "@maniumek/suwfrontkit";

import { api } from "@/api/client";
import type { Bot } from "@/api/bots";
import type { components } from "@/api/openapi";

export function registerBot(bot: components["schemas"]["RegisterBot"]): Promise<Bot> {
    return dataOf(api.POST("/api/bots", { body: bot }));
}
