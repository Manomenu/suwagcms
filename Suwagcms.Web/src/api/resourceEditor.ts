import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { ApiError } from "./client";
import { useResourceHistory } from "./history";
import { DISCARD_QUESTION, mayLeave, useLeaveGuard } from "./leaveGuard";
import { renamer } from "./rename";
import { revisionOf, type ResourceApi } from "./resourceApi";
import { usageHref, useUsage, type UsageCollection, type UsageLink } from "./usage";

function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

/**
 * What a screen listing one kind of resource keeps: the list, which one is open, and everything around its form. The list is
 * loaded when the screen opens, so resources added meanwhile from the CLI or another tab are found. A link from another
 * screen or the CLI arrives as `?open=<id>`; it is taken once and dropped from the address.
 */
export function useResourceEditor<View extends { revision: number | string }, Save>({
    bot,
    api,
    idOf,
    usage,
    list,
    revisionOfOpened = false,
    onRenamed,
}: {
    bot: string;
    api: ResourceApi<View, Save>;
    idOf: (view: View) => string;
    /** The collection whose Used by and Uses lists to show; null when the kind has none. */
    usage: UsageCollection | null;
    /** How to fetch the list, when the screen narrows it. */
    list?: (signal: AbortSignal) => Promise<View[]>;
    /** A narrowed list may not hold the open resource, so only what was opened tells its revision. */
    revisionOfOpened?: boolean;
    onRenamed?: () => void;
}) {
    const [items, setItems] = useState<View[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<{ item: View | null } | null>(null);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [location, navigate] = useLocation();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    useLeaveGuard(dirty, DISCARD_QUESTION);

    const item = open?.item ?? null;
    const id = item === null ? null : idOf(item);
    const shown = revisionOfOpened ? item : (items?.find((other) => idOf(other) === id) ?? item);
    const revision = shown === null ? 0 : revisionOf(shown);
    const used = useUsage(bot, usage ?? "facts", usage === null ? null : id, revision);
    const history = useResourceHistory(bot, api.collection, id, revision);
    const changer = useMemo(() => renamer(bot, api.collection, id ?? "", revision), [bot, api.collection, id, revision]);

    const fetchList = useCallback((signal: AbortSignal) => list?.(signal) ?? api.list(bot, signal), [list, api, bot]);

    const reload = useCallback(() => {
        const controller = new AbortController();
        fetchList(controller.signal)
            .then(setItems)
            .catch((error: unknown) => {
                setFailure(messageOf(error));
            });
    }, [fetchList]);

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchList(controller.signal)
            .then(setItems)
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(messageOf(error));
            });
        return () => {
            controller.abort();
        };
    }, [fetchList]);

    const openById = useCallback(
        (wanted: string) => {
            api.get(bot, wanted, new AbortController().signal)
                .then((found) => {
                    setOpen({ item: found });
                })
                .catch((error: unknown) => {
                    setFailure(messageOf(error));
                });
        },
        [api, bot],
    );

    useEffect(() => {
        if (openParam === null) return;
        navigate(location, { replace: true });
        if (!dirty || window.confirm(DISCARD_QUESTION)) openById(openParam);
    }, [openParam, location, navigate, dirty, openById]);

    function close() {
        setOpen(null);
    }

    function remove(view: View) {
        setDeleteFailure(null);
        api.remove(bot, idOf(view), revisionOf(view))
            .then(() => {
                close();
                reload();
            })
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...error.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`)].join(" ")
                        : String(error),
                );
            });
    }

    return {
        items,
        failure,
        item,
        isOpen: open !== null,
        revision,
        reload,
        openById,
        close,
        remove,
        // Why: opening another form would throw away what was typed, so it asks first, as leaving the screen does.
        add: () => {
            if (mayLeave()) setOpen({ item: null });
        },
        edit: (view: View) => {
            if (mayLeave()) setOpen({ item: view });
        },
        mayLeave,
        formProps: {
            editing: item !== null,
            revision,
            onSaved: () => {
                close();
                reload();
            },
            onReload: reload,
            onCancel: close,
            onDirtyChange: setDirty,
        },
        panels: {
            editedId: id,
            deleteFailure,
            dirty,
            changingId,
            onChangingId: setChangingId,
            preview: changer.preview,
            apply: changer.apply,
            onChanged: (newId: string) => {
                setChangingId(false);
                close();
                reload();
                onRenamed?.();
                navigate(`${location}?open=${encodeURIComponent(newId)}`);
            },
            usage:
                usage === null ? null : { usage: used.usage, failure: used.failure, hrefOf: (entry: UsageLink) => usageHref(bot, entry) },
            onDelete: () => {
                if (item !== null) remove(item);
            },
            history,
        },
    };
}
