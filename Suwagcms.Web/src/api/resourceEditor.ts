import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { ApiError } from "./client";
import { useResourceHistory } from "./history";
import { DISCARD_QUESTION, mayLeave, useLeaveGuard } from "./leaveGuard";
import { renamer } from "./rename";
import { addressOf, editHref, listHref, newHref } from "./resourceAddress";
import { revisionOf, type ResourceApi } from "./resourceApi";
import { usageHref, useUsage, type UsageCollection, type UsageLink } from "./usage";

function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

/**
 * What a screen listing one kind of resource keeps. The address says what is open, so a reload or a sent link lands on the same
 * screen. The list is loaded when the screen opens, so resources added meanwhile from the CLI or another tab are found. An older
 * link, `?open=<id>`, which the CLI still prints, is replaced by the edit address.
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
    const [opened, setOpened] = useState<{ id: string; item: View | null } | null>(null);
    const [deleteFailure, setDeleteFailure] = useState<{ id: string; message: string } | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [location, navigate] = useLocation();
    const search = useSearch();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    useLeaveGuard(dirty, DISCARD_QUESTION);

    const address = addressOf(location, api.collection);
    const wanted = address.screen === "edit" ? address.id : null;
    const current = opened !== null && opened.id === wanted ? opened : null;
    const item = current?.item ?? null;
    const id = item === null ? null : idOf(item);
    const shown = revisionOfOpened ? item : (items?.find((other) => idOf(other) === id) ?? item);
    const revision = shown === null ? 0 : revisionOf(shown);
    const used = useUsage(bot, usage ?? "facts", usage === null ? null : id, revision);
    const history = useResourceHistory(bot, api.collection, id, revision);
    const changer = useMemo(() => renamer(bot, api.collection, id ?? "", revision), [bot, api.collection, id, revision]);

    // Why: what narrows the list travels with the screens it opens, so coming back finds the list as it was left.
    const withSearch = (href: string) => (search === "" || openParam !== null ? href : `${href}?${search}`);

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

    const fetchOpened = useCallback(
        (openedId: string, signal: AbortSignal) => {
            api.get(bot, openedId, signal)
                .then((found) => {
                    setOpened({ id: openedId, item: found });
                })
                .catch((error: unknown) => {
                    if (signal.aborted) return;
                    if (error instanceof ApiError && error.status === 404) setOpened({ id: openedId, item: null });
                    else setFailure(messageOf(error));
                });
        },
        [api, bot],
    );

    useEffect(() => {
        if (wanted === null) return undefined;
        const controller = new AbortController();
        fetchOpened(wanted, controller.signal);
        return () => {
            controller.abort();
            // Why: a form starts from what it is given, so it must never open on what was read the last time.
            setOpened(null);
        };
    }, [wanted, fetchOpened]);

    useEffect(() => {
        if (openParam !== null) navigate(editHref(bot, api.collection, openParam), { replace: true });
    }, [openParam, navigate, bot, api.collection]);

    // Why: replacing the address keeps Back from reopening what was just saved or deleted.
    function close() {
        setDeleteFailure(null);
        navigate(withSearch(listHref(bot, api.collection)), { replace: true });
    }

    function remove(view: View) {
        const removed = idOf(view);
        setDeleteFailure(null);
        api.remove(bot, removed, revisionOf(view))
            .then(() => {
                close();
                reload();
            })
            .catch((error: unknown) => {
                setDeleteFailure({
                    id: removed,
                    message:
                        error instanceof ApiError
                            ? [error.message, ...error.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`)].join(" ")
                            : String(error),
                });
            });
    }

    return {
        items,
        failure,
        item,
        reload,
        /** Opens another resource, or reads the open one again. */
        openById: (other: string) => {
            if (other === wanted) fetchOpened(other, new AbortController().signal);
            else navigate(withSearch(editHref(bot, api.collection, other)));
        },
        close,
        isOpen: address.screen !== "list",
        // Why: opening another form would throw away what was typed, so it asks first, as leaving the screen does.
        add: () => {
            if (mayLeave()) navigate(withSearch(newHref(bot, api.collection)));
        },
        edit: (view: View) => {
            if (mayLeave()) navigate(withSearch(editHref(bot, api.collection, idOf(view))));
        },
        mayLeave,
        formProps: {
            editing: address.screen === "edit",
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
            onBack: () => {
                if (mayLeave()) close();
            },
            ready: address.screen === "new" || item !== null,
            missingId: current !== null && current.item === null ? current.id : null,
            editedId: id,
            deleteFailure: deleteFailure !== null && deleteFailure.id === id ? deleteFailure.message : null,
            dirty,
            changingId,
            onChangingId: setChangingId,
            preview: changer.preview,
            apply: changer.apply,
            onChanged: (newId: string) => {
                setChangingId(false);
                reload();
                onRenamed?.();
                navigate(withSearch(editHref(bot, api.collection, newId)), { replace: true });
            },
            usage:
                usage === null
                    ? null
                    : {
                          usage: used.usage,
                          failure: used.failure,
                          hrefOf: (entry: UsageLink) => usageHref(bot, entry),
                          onFollow: (event: React.MouseEvent) => {
                              if (!mayLeave()) event.preventDefault();
                          },
                      },
            onDelete: () => {
                if (item !== null) remove(item);
            },
            history,
        },
    };
}
