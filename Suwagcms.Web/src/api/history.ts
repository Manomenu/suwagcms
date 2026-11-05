// Why: the resource screens and the bot's History page all read the same two routes, so the calls and their hook exist once.
import { useCallback, useEffect, useState } from "react";

import { request } from "./client";
import type { components } from "./openapi";

type HistoryPage = components["schemas"]["HistoryPage"];
export type HistoryEntry = components["schemas"]["HistoryEntry"];

export type HistoryCollection = "facts" | "knowledge-bases" | "articles" | "procedures" | "actions" | "persona" | "versions";

const COLLECTIONS: Record<string, HistoryCollection> = {
    fact: "facts",
    knowledgeBase: "knowledge-bases",
    article: "articles",
    procedure: "procedures",
    action: "actions",
    persona: "persona",
    version: "versions",
};

export function isHistoryCollection(value: string | null): value is HistoryCollection {
    return value !== null && Object.values(COLLECTIONS).includes(value as HistoryCollection);
}

/** The page of one resource's history, which is found the same way after the resource is deleted. */
export function historyHref(bot: string, entry: Pick<HistoryEntry, "kind" | "resourceId">): string {
    return `~/bots/${encodeURIComponent(bot)}/history?kind=${COLLECTIONS[entry.kind] ?? "articles"}&id=${encodeURIComponent(entry.resourceId)}`;
}

export interface HistoryState {
    entries: HistoryEntry[] | null;
    since: string | null;
    hasOlder: boolean;
    failure: string | null;
    onOlder: () => void;
}

function useHistoryAt(path: string | null, revision: number): HistoryState {
    const key = `${path ?? ""}/${revision.toString()}`;
    const [answer, setAnswer] = useState<{ key: string; pages: HistoryPage[]; failure: string | null } | null>(null);

    useEffect(() => {
        if (path === null) return undefined;
        const controller = new AbortController();
        request<HistoryPage>(path, { signal: controller.signal })
            .then((page) => {
                setAnswer({ key, pages: [page], failure: null });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted)
                    setAnswer({ key, pages: [], failure: error instanceof Error ? error.message : String(error) });
            });
        return () => {
            controller.abort();
        };
    }, [path, key]);

    // Why: an answer for another resource or revision is stale, so it reads as "still loading" instead of showing the wrong entries.
    const current = answer?.key === key ? answer : null;
    const next = current?.pages.at(-1)?.next ?? null;

    const onOlder = useCallback(() => {
        if (path === null || next === null) return;
        request<HistoryPage>(`${path}?before=${String(next)}`)
            .then((page) => {
                setAnswer((was) => (was?.key === key ? { ...was, pages: [...was.pages, page] } : was));
            })
            .catch((error: unknown) => {
                setAnswer((was) => (was?.key === key ? { ...was, failure: error instanceof Error ? error.message : String(error) } : was));
            });
    }, [path, key, next]);

    return {
        entries: current === null || current.pages.length === 0 ? null : current.pages.flatMap((page) => page.entries),
        since: current?.pages[0]?.since ?? null,
        hasOlder: next !== null,
        failure: current?.failure ?? null,
        onOlder,
    };
}

/** Every change to the bot, newest first. */
export function useBotHistory(bot: string): HistoryState {
    return useHistoryAt(`/bots/${encodeURIComponent(bot)}/history`, 0);
}

/** The history of one resource; `id` null loads nothing. A changed `revision` asks again, as a save does. */
export function useResourceHistory(bot: string, collection: HistoryCollection, id: string | null, revision = 0): HistoryState {
    return useHistoryAt(id === null ? null : `/bots/${encodeURIComponent(bot)}/${collection}/${encodeURIComponent(id)}/history`, revision);
}
