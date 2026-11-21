import { dataOf, type UsageLink as PanelLink } from "@maniumek/suwfrontkit";
import { skipToken, useQuery } from "@tanstack/react-query";

import { api, messageOf } from "./client";
import type { components } from "./openapi";
import { editHref } from "./resourceAddress";

type Usage = components["schemas"]["UsageView"];
export type UsageLink = components["schemas"]["UsageLink"];

export type UsageCollection = "facts" | "articles" | "procedures" | "actions";

export function usageHref(bot: string, link: UsageLink): string {
    const collection =
        link.kind === "procedure" ? "procedures" : link.kind === "fact" ? "facts" : link.kind === "action" ? "actions" : "articles";
    return editHref(bot, collection, link.id);
}

export function usageLinkOf(bot: string): (link: UsageLink) => PanelLink {
    return (link) => ({ href: usageHref(bot, link), label: link.id, detail: `${link.kind} · ${link.field}` });
}

export interface UsageState {
    usage: Usage | null;
    failure: string | null;
}

export function useUsage(bot: string, collection: UsageCollection, id: string | null, revision = 0): UsageState {
    const usage = useQuery({
        queryKey: ["usage", bot, collection, id, revision],
        queryFn:
            id === null
                ? skipToken
                : ({ signal }) =>
                      dataOf(api.GET("/api/bots/{bot}/{kind}/{id}/usage", { params: { path: { bot, kind: collection, id } }, signal })),
    });
    return { usage: usage.data ?? null, failure: usage.error === null ? null : messageOf(usage.error) };
}
