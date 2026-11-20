import { useEffect, useState } from "react";

import { request } from "./client";
import type { components } from "./openapi";
import { editHref } from "./resourceAddress";

type Usage = components["schemas"]["UsageView"];
export type UsageLink = components["schemas"]["UsageLink"];

export type UsageCollection = "facts" | "articles" | "procedures" | "actions";

function fetchUsage(bot: string, collection: UsageCollection, id: string, signal: AbortSignal): Promise<Usage> {
    return request<Usage>(`/bots/${encodeURIComponent(bot)}/${collection}/${encodeURIComponent(id)}/usage`, { signal });
}

export function usageHref(bot: string, link: UsageLink): string {
    const collection =
        link.kind === "procedure" ? "procedures" : link.kind === "fact" ? "facts" : link.kind === "action" ? "actions" : "articles";
    return `~${editHref(bot, collection, link.id)}`;
}

export interface UsageState {
    usage: Usage | null;
    failure: string | null;
}

export function useUsage(bot: string, collection: UsageCollection, id: string | null, revision = 0): UsageState {
    const key = `${bot}/${collection}/${id ?? ""}/${revision}`;
    const [answer, setAnswer] = useState<{ key: string; usage: Usage | null; failure: string | null } | null>(null);

    useEffect(() => {
        if (id === null) return undefined;
        const controller = new AbortController();
        fetchUsage(bot, collection, id, controller.signal)
            .then((usage) => {
                setAnswer({ key, usage, failure: null });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted)
                    setAnswer({ key, usage: null, failure: error instanceof Error ? error.message : String(error) });
            });
        return () => {
            controller.abort();
        };
    }, [bot, collection, id, revision, key]);

    return answer?.key === key ? { usage: answer.usage, failure: answer.failure } : { usage: null, failure: null };
}
