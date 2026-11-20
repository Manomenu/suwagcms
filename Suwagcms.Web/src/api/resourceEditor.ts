import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { ApiError } from "./client";
import { useKindHistory, useResourceHistory } from "./history";
import { mayLeave, useLeaveGuardActive } from "./leaveGuard";
import { renamer } from "./rename";
import { addressOf, editHref, listHref, newHref, withListSearch } from "./resourceAddress";
import { revisionOf, type ResourceApi } from "./resourceApi";
import { usageHref, useUsage, type UsageCollection, type UsageLink } from "./usage";

function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

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
    usage: UsageCollection | null;
    list?: (signal: AbortSignal) => Promise<View[]>;
    revisionOfOpened?: boolean;
    onRenamed?: () => void;
}) {
    const [items, setItems] = useState<View[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [opened, setOpened] = useState<{ id: string; item: View | null } | null>(null);
    const [deleteFailure, setDeleteFailure] = useState<{ id: string; message: string } | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [location, navigate] = useLocation();
    const search = useSearch();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    const dirty = useLeaveGuardActive();

    const address = addressOf(location, api.collection);
    const wanted = address.screen === "edit" ? address.id : null;
    const current = opened !== null && opened.id === wanted ? opened : null;
    const item = current?.item ?? null;
    const id = item === null ? null : idOf(item);
    const shown = revisionOfOpened ? item : (items?.find((other) => idOf(other) === id) ?? item);
    const revision = shown === null ? 0 : revisionOf(shown);
    const used = useUsage(bot, usage ?? "facts", usage === null ? null : id, revision);
    const history = useResourceHistory(bot, api.collection, id, revision);
    const listHistory = useKindHistory(bot, address.screen === "list" ? api.collection : null);
    const changer = useMemo(() => renamer(bot, api.collection, id ?? "", revision), [bot, api.collection, id, revision]);

    const withSearch = (href: string) => withListSearch(href, search);

    const fetchList = useCallback((signal: AbortSignal) => list?.(signal) ?? api.list(bot, signal), [list, api, bot]);

    const reload = () => {
        fetchList(new AbortController().signal)
            .then(setItems)
            .catch((error: unknown) => {
                setFailure(messageOf(error));
            });
    };

    useEffect(() => {
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
            setOpened(null);
        };
    }, [wanted, fetchOpened]);

    function close() {
        setDeleteFailure(null);
        navigate(withSearch(listHref(bot, api.collection)), { replace: true });
    }

    function remove(view: View) {
        const removed = idOf(view);
        setDeleteFailure(null);
        return api
            .remove(bot, removed, revisionOf(view))
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
        redirect: openParam === null ? null : editHref(bot, api.collection, openParam),
        items,
        failure,
        item,
        reload,
        openById: (other: string) => {
            if (other === wanted) fetchOpened(other, new AbortController().signal);
            else navigate(withSearch(editHref(bot, api.collection, other)));
        },
        close,
        isOpen: address.screen !== "list",
        listHistory,
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
            onDelete: () => (item === null ? Promise.resolve() : remove(item)),
            history,
        },
    };
}
