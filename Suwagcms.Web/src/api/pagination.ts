// Why: the address says which page a person is on and the server says how long a page is, so a screen only draws what this module works out.
import { createContext, useContext, useEffect, useState } from "react";
import { useSearchParams } from "wouter";

import { request } from "./client";
import type { components } from "./openapi";

type ServerSettings = components["schemas"]["ServerSettings"];

interface PageSize {
    size: number | null;
    failure: string | null;
}

export const PageSizeContext = createContext<PageSize>({ size: null, failure: null });

/** Asks the server how long a page is; the one provider of the app holds the answer for every screen. */
export function useAskedPageSize(): PageSize {
    const [answer, setAnswer] = useState<PageSize>({ size: null, failure: null });
    useEffect(() => {
        const controller = new AbortController();
        request<ServerSettings>("/settings", { signal: controller.signal })
            .then((settings) => {
                setAnswer({ size: Number(settings.pageSize), failure: null });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setAnswer({ size: null, failure: error instanceof Error ? error.message : String(error) });
            });
        return () => {
            controller.abort();
        };
    }, []);
    return answer;
}

/** Where a list stands, as the shared controls need it. */
export interface Paging {
    page: number;
    pages: number;
    asking: boolean;
    failure: string | null;
    onMove: (page: number) => void;
}

// Why: the API takes a page as a 32-bit number, so anything longer in an address reads as no page at all rather than as a refused request.
const LARGEST_PAGE = 2_147_483_647;

/** The page an address asks for: a whole number, 1 or more; anything else is the first. */
export function pageAsked(raw: string | null): number {
    if (raw === null || !/^[1-9]\d*$/.test(raw)) return 1;
    const page = Number(raw);
    return page <= LARGEST_PAGE ? page : 1;
}

export function lastPage(rows: number, size: number): number {
    return Math.max(1, Math.ceil(rows / size));
}

export function rowsOfPage<Row>(rows: Row[], page: number, size: number): Row[] {
    return rows.slice((page - 1) * size, page * size);
}

type SetParams = ReturnType<typeof useSearchParams>[1];

function writePage(setParams: SetParams, name: string, page: number, replace: boolean) {
    setParams(
        (was) => {
            const next = new URLSearchParams(was);
            if (page === 1) next.delete(name);
            else next.set(name, String(page));
            return next;
        },
        { replace },
    );
}

/** Goes back to the first page in place, for when what is listed has just changed. */
export function useToFirstPage(name = "page"): () => void {
    const [, setParams] = useSearchParams();
    return () => {
        writePage(setParams, name, 1, true);
    };
}

/**
 * The page under one name in the address, held within `last` once it is known. An address that asks for something else is replaced,
 * so Back never returns to a page that does not exist; a move is a step of its own, which Back undoes.
 */
export function usePageInAddress(name: string, last: number | null): { page: number; move: (page: number) => void } {
    const [params, setParams] = useSearchParams();
    const raw = params.get(name);
    const asked = pageAsked(raw);
    const page = last === null ? asked : Math.min(asked, last);
    const written = page === 1 ? null : String(page);
    useEffect(() => {
        if (raw !== written) writePage(setParams, name, page, true);
    }, [raw, written, setParams, name, page]);
    return {
        page,
        move: (next) => {
            writePage(setParams, name, next, false);
        },
    };
}

/** A whole list divided here in the browser. While the size is unknown it hands back every row, so an empty list can still say it is empty. */
export function usePagedRows<Row>(rows: Row[]): { rows: Row[]; paging: Paging } {
    const { size, failure } = useContext(PageSizeContext);
    const pages = size === null ? null : lastPage(rows.length, size);
    const { page, move } = usePageInAddress("page", pages);
    return {
        rows: size === null ? rows : rowsOfPage(rows, page, size),
        paging: { page, pages: pages ?? 1, asking: size === null && failure === null, failure, onMove: move },
    };
}
