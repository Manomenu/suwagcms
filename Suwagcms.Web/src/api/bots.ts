import { dataOf } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";

import { api, messageOf } from "./client";
import type { components } from "./openapi";

export type Bot = components["schemas"]["Bot"];

export const BOTS = ["bots"];

export function useBots(): { bots: Bot[] | null; failure: string | null } {
    const bots = useQuery({ queryKey: BOTS, queryFn: ({ signal }) => dataOf(api.GET("/api/bots", { signal })) });
    return { bots: bots.data ?? null, failure: bots.error === null ? null : messageOf(bots.error) };
}
