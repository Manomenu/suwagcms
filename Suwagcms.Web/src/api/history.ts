import { useContext, useEffect, useState } from "react";

import { request } from "./client";
import type { components } from "./openapi";
import { lastPage, PageSizeContext, usePageInAddress, type Paging } from "./pagination";
import { HISTORY_PAGE } from "./resourceAddress";

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

export function historyHref(bot: string, entry: Pick<HistoryEntry, "kind" | "resourceId">): string {
    return `~/bots/${encodeURIComponent(bot)}/history?kind=${COLLECTIONS[entry.kind] ?? "articles"}&id=${encodeURIComponent(entry.resourceId)}`;
}

export interface HistoryState {
    entries: HistoryEntry[] | null;
    since: string | null;
    failure: string | null;
    paging: Paging;
    incomplete: boolean;
}

function useHistoryAt(path: string | null, revision: number, pageName: string, filter = ""): HistoryState {
    const { size, failure: sizeFailure } = useContext(PageSizeContext);
    const asked = `${path ?? ""}${filter}/${revision.toString()}/${String(size)}`;
    const [answer, setAnswer] = useState<{ asked: string; page: number; body: HistoryPage | null; failure: string | null } | null>(null);
    const known = answer?.asked === asked ? answer : null;
    const pages = known?.body && size !== null ? lastPage(Number(known.body.total ?? 0), size) : null;
    const { page, move } = usePageInAddress(pageName, pages, revision);

    useEffect(() => {
        if (path === null || size === null) return undefined;
        const controller = new AbortController();
        request<HistoryPage>(`${path}?limit=${size}&page=${page}${filter}`, { signal: controller.signal })
            .then((body) => {
                setAnswer({ asked, page, body, failure: null });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted)
                    setAnswer({ asked, page, body: null, failure: error instanceof Error ? error.message : String(error) });
            });
        return () => {
            controller.abort();
        };
    }, [path, filter, size, page, asked]);

    const current = known?.page === page ? known : null;
    return {
        entries: current?.body?.entries ?? null,
        since: current?.body?.since ?? null,
        failure: sizeFailure ?? current?.failure ?? null,
        paging: { page, pages: pages ?? 1, asking: false, failure: null, onMove: move },
        incomplete: current?.body?.incomplete ?? false,
    };
}

export function useBotHistory(bot: string): HistoryState {
    return useHistoryAt(`/bots/${encodeURIComponent(bot)}/history`, 0, "page");
}

export type HistoryOfMany = HistoryState & { hrefOf: (line: Pick<HistoryEntry, "kind" | "resourceId">) => string };

export function useKindHistory(bot: string, collection: HistoryCollection | null, revision = 0): HistoryOfMany {
    const history = useHistoryAt(
        collection === null ? null : `/bots/${encodeURIComponent(bot)}/history`,
        revision,
        HISTORY_PAGE,
        collection === null ? "" : `&kind=${collection}`,
    );
    return {
        ...history,
        hrefOf: (line) => historyHref(bot, line),
    };
}

export function useResourceHistory(
    bot: string,
    collection: HistoryCollection,
    id: string | null,
    revision = 0,
    pageName = HISTORY_PAGE,
): HistoryState {
    return useHistoryAt(
        id === null ? null : `/bots/${encodeURIComponent(bot)}/${collection}/${encodeURIComponent(id)}/history`,
        revision,
        pageName,
    );
}

export function useVersionWindow(bot: string, number: number | null): HistoryState {
    return useHistoryAt(number === null ? null : `/bots/${encodeURIComponent(bot)}/versions/${number}/window`, 0, HISTORY_PAGE);
}
