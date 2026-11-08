// Why: the resource screens, the bot's History page and a version's page all read the same kind of answer, so the calls and their hook exist once.
import { useContext, useEffect, useRef, useState } from "react";

import { request } from "./client";
import type { components } from "./openapi";
import { lastPage, PageSizeContext, usePageInAddress, useToFirstPage, type Paging } from "./pagination";
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

/** The page of one resource's history, which is found the same way after the resource is deleted. */
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
    const { page, move } = usePageInAddress(pageName, pages);
    const toFirstPage = useToFirstPage(pageName);

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

    // Why: a save or a rename adds an entry, which is on the newest page; 0 is only the revision before the resource has loaded.
    const seenRevision = useRef(revision);
    useEffect(() => {
        if (seenRevision.current !== 0 && seenRevision.current !== revision) toFirstPage();
        seenRevision.current = revision;
    });

    // Why: an answer for another resource, revision or page is stale, so it reads as "still loading" instead of showing the wrong entries.
    const current = known?.page === page ? known : null;
    return {
        entries: current?.body?.entries ?? null,
        since: current?.body?.since ?? null,
        failure: sizeFailure ?? current?.failure ?? null,
        paging: { page, pages: pages ?? 1, asking: false, failure: null, onMove: move },
        incomplete: current?.body?.incomplete ?? false,
    };
}

/** Every change to the bot, newest first, under `page` in the address. */
export function useBotHistory(bot: string): HistoryState {
    return useHistoryAt(`/bots/${encodeURIComponent(bot)}/history`, 0, "page");
}

/** What a page's History panel is given: the entries and, where they reach several resources, the link of each to its history. */
export type HistoryOfMany = HistoryState & { hrefOf: (line: Pick<HistoryEntry, "kind" | "resourceId">) => string };

/**
 * The changes of one kind of resource, every one of it, newest first, for the panel under that kind's list, under `historyPage` so
 * it never moves the list's `page`. `collection` null loads nothing; a changed `revision` asks again, as creating a version does.
 */
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

/**
 * The history of one resource; `id` null loads nothing. A changed `revision` asks again, as a save does. On a page of its own it is
 * under `page`; beside a form, whose address holds the `page` of the list it came from, it is under its own name.
 */
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

/** What happened between the previous version and this one; the server works it out. `number` null loads nothing. */
export function useVersionWindow(bot: string, number: number | null): HistoryState {
    return useHistoryAt(number === null ? null : `/bots/${encodeURIComponent(bot)}/versions/${number}/window`, 0, HISTORY_PAGE);
}
