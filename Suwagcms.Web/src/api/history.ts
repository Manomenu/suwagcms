import { dataOf } from "@maniumek/suwfrontkit";
import { skipToken, useQuery } from "@tanstack/react-query";
import { useContext, useState } from "react";

import { api, messageOf } from "./client";
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

export function botHistoryHref(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/history`;
}

export function historyHref(bot: string, entry: Pick<HistoryEntry, "kind" | "resourceId">): string {
    return `/bots/${encodeURIComponent(bot)}/history?kind=${COLLECTIONS[entry.kind] ?? "articles"}&id=${encodeURIComponent(entry.resourceId)}`;
}

export interface HistoryState {
    entries: HistoryEntry[] | null;
    since: string | null;
    failure: string | null;
    paging: Paging;
    incomplete: boolean;
}

type PageOf = (limit: number, page: number, signal: AbortSignal) => Promise<HistoryPage>;

interface HistorySource {
    key: unknown[];
    pageOf: PageOf;
}

function useHistoryAt(source: HistorySource | null, revision: number, pageName: string): HistoryState {
    const { size, failure: sizeFailure } = useContext(PageSizeContext);
    const asked = JSON.stringify([source?.key ?? null, revision, size]);
    const [known, setKnown] = useState<{ asked: string; total: number } | null>(null);
    const pages = known?.asked === asked && size !== null ? lastPage(known.total, size) : null;
    const { page, move } = usePageInAddress(pageName, pages, revision);
    const shown = useQuery({
        queryKey: ["history", asked, page],
        queryFn: source === null || size === null ? skipToken : ({ signal }) => source.pageOf(size, page, signal),
    });
    const total = shown.data === undefined ? null : Number(shown.data.total ?? 0);
    if (total !== null && (known?.asked !== asked || known.total !== total)) setKnown({ asked, total });

    return {
        entries: shown.data?.entries ?? null,
        since: shown.data?.since ?? null,
        failure: sizeFailure ?? (shown.error === null ? null : messageOf(shown.error)),
        paging: { page, pages: pages ?? 1, asking: false, failure: null, onMove: move },
        incomplete: shown.data?.incomplete ?? false,
    };
}

function botHistory(bot: string, kind: HistoryCollection | null): HistorySource {
    return {
        key: [bot, kind],
        pageOf: (limit, page, signal) =>
            dataOf(
                api.GET("/api/bots/{bot}/history", {
                    params: { path: { bot }, query: kind === null ? { limit, page } : { limit, page, kind } },
                    signal,
                }),
            ),
    };
}

export function useBotHistory(bot: string): HistoryState {
    return useHistoryAt(botHistory(bot, null), 0, "page");
}

export type HistoryOfMany = HistoryState & { hrefOf: (line: Pick<HistoryEntry, "kind" | "resourceId">) => string };

export function useKindHistory(bot: string, collection: HistoryCollection | null, revision = 0): HistoryOfMany {
    const history = useHistoryAt(collection === null ? null : botHistory(bot, collection), revision, HISTORY_PAGE);
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
        id === null
            ? null
            : {
                  key: [bot, collection, id],
                  pageOf: (limit, page, signal) =>
                      dataOf(
                          api.GET("/api/bots/{bot}/{kind}/{id}/history", {
                              params: { path: { bot, kind: collection, id }, query: { limit, page } },
                              signal,
                          }),
                      ),
              },
        revision,
        pageName,
    );
}

export function useVersionWindow(bot: string, number: number | null): HistoryState {
    return useHistoryAt(
        number === null
            ? null
            : {
                  key: [bot, "window", number],
                  pageOf: (limit, page, signal) =>
                      dataOf(
                          api.GET("/api/bots/{bot}/versions/{number}/window", {
                              params: { path: { bot, number }, query: { limit, page } },
                              signal,
                          }),
                      ),
              },
        0,
        HISTORY_PAGE,
    );
}
