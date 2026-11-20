import { dataOf } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useEffect, useState } from "react";
import { useSearchParams } from "wouter";

import { api, messageOf } from "./client";

interface PageSize {
    size: number | null;
    failure: string | null;
}

export const PageSizeContext = createContext<PageSize>({ size: null, failure: null });

export function useAskedPageSize(): PageSize {
    const settings = useQuery({
        queryKey: ["settings"],
        queryFn: ({ signal }) => dataOf(api.GET("/api/settings", { signal })),
        staleTime: Infinity,
    });
    return {
        size: settings.data === undefined ? null : Number(settings.data.pageSize),
        failure: settings.error === null ? null : messageOf(settings.error),
    };
}

export interface Paging {
    page: number;
    pages: number;
    asking: boolean;
    failure: string | null;
    onMove: (page: number) => void;
}

const LARGEST_PAGE = 2_147_483_647;

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

export function useToFirstPage(name = "page"): () => void {
    const [, setParams] = useSearchParams();
    return () => {
        writePage(setParams, name, 1, true);
    };
}

export function usePageInAddress(name: string, last: number | null, revision = 0): { page: number; move: (page: number) => void } {
    const [params, setParams] = useSearchParams();
    const raw = params.get(name);
    const [seen, setSeen] = useState<{ revision: number; outdated: string | null }>({ revision, outdated: null });
    if (seen.revision !== revision) setSeen({ revision, outdated: seen.revision === 0 ? null : raw });
    else if (seen.outdated !== null && raw !== seen.outdated) setSeen({ revision, outdated: null });
    const asked = raw !== null && raw === seen.outdated ? 1 : pageAsked(raw);
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

export function usePagedRows<Row>(rows: Row[]): { rows: Row[]; paging: Paging } {
    const { size, failure } = useContext(PageSizeContext);
    const pages = size === null ? null : lastPage(rows.length, size);
    const { page, move } = usePageInAddress("page", pages);
    return {
        rows: size === null ? rows : rowsOfPage(rows, page, size),
        paging: { page, pages: pages ?? 1, asking: size === null && failure === null, failure, onMove: move },
    };
}
