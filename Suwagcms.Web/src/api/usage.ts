// Why: the fact, article and procedure screens all ask the same question of the same route, so the call and its hook exist once.
import { useEffect, useState } from "react";

import { request } from "./client";
import type { components } from "./openapi";

type Usage = components["schemas"]["UsageView"];
export type UsageLink = components["schemas"]["UsageLink"];

export type UsageCollection = "facts" | "articles" | "procedures";

function fetchUsage(bot: string, collection: UsageCollection, id: string, signal: AbortSignal): Promise<Usage> {
    return request<Usage>(`/bots/${encodeURIComponent(bot)}/${collection}/${encodeURIComponent(id)}/usage`, { signal });
}

/** Where the web app shows a resource: a fact or an article opens in its list from `?open=<id>`, a procedure in its creator. */
export function usageHref(bot: string, link: UsageLink): string {
    const base = `~/bots/${encodeURIComponent(bot)}`;
    const id = encodeURIComponent(link.id);
    if (link.kind === "procedure") return `${base}/procedures/${id}`;
    return `${base}/${link.kind === "fact" ? "facts" : "articles"}?open=${id}`;
}

export interface UsageState {
    usage: Usage | null;
    failure: string | null;
}

/** Loads the used-by and uses lists of one resource; `id` null loads nothing. A changed `revision` asks again, as a save does. */
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

    // Why: an answer for another resource or revision is stale, so it reads as "still loading" instead of showing the wrong lists.
    return answer?.key === key ? { usage: answer.usage, failure: answer.failure } : { usage: null, failure: null };
}
