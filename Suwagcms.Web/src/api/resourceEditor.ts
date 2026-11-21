import { ApiError, mayLeave, useLeaveGuardActive } from "@maniumek/suwfrontkit";
import { skipToken, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { messageOf, mistakesOf } from "./client";
import { useKindHistory, useResourceHistory } from "./history";
import { renamer } from "./rename";
import { addressOf, editHref, listHref, newHref, withListSearch } from "./resourceAddress";
import { revisionOf, type ResourceApi } from "./resourceApi";
import { usageHref, useUsage, type UsageCollection, type UsageLink } from "./usage";

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
    list?: { key: unknown; fetch: (signal: AbortSignal) => Promise<View[]> };
    revisionOfOpened?: boolean;
    onRenamed?: () => void;
}) {
    const [deleteFailure, setDeleteFailure] = useState<{ id: string; message: string } | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [location, navigate] = useLocation();
    const search = useSearch();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    const dirty = useLeaveGuardActive();

    const address = addressOf(location, api.collection);
    const wanted = address.screen === "edit" ? address.id : null;
    const queries = useQueryClient();
    const resources = ["resources", bot, api.collection];
    const listed = useQuery({
        queryKey: [...resources, "list", list?.key ?? null],
        queryFn: ({ signal }) => list?.fetch(signal) ?? api.list(bot, signal),
    });
    const opened = useQuery({
        queryKey: [...resources, "item", wanted],
        queryFn: wanted === null ? skipToken : ({ signal }) => api.get(bot, wanted, signal),
    });
    const items = listed.data ?? null;
    const missing = opened.error instanceof ApiError && opened.error.status === 404;
    const openFailure = opened.error === null || missing ? null : messageOf(opened.error);
    const failure = listed.error === null ? openFailure : messageOf(listed.error);
    const current =
        wanted === null
            ? null
            : missing
              ? { id: wanted, item: null }
              : opened.data === undefined
                ? null
                : { id: wanted, item: opened.data };
    const item = current?.item ?? null;
    const id = item === null ? null : idOf(item);
    const shown = revisionOfOpened ? item : (items?.find((other) => idOf(other) === id) ?? item);
    const revision = shown === null ? 0 : revisionOf(shown);
    const used = useUsage(bot, usage ?? "facts", usage === null ? null : id, revision);
    const history = useResourceHistory(bot, api.collection, id, revision);
    const listHistory = useKindHistory(bot, address.screen === "list" ? api.collection : null);
    const changer = useMemo(() => renamer(bot, api.collection, id ?? "", revision), [bot, api.collection, id, revision]);

    const withSearch = (href: string) => withListSearch(href, search);

    const reload = () => {
        void queries.invalidateQueries({ queryKey: resources });
        void queries.invalidateQueries({ queryKey: ["history"] });
    };

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
                            ? [error.message, ...mistakesOf(error).map((mistake) => `${mistake.message} ${mistake.fix}`)].join(" ")
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
            if (other === wanted) void opened.refetch();
            else navigate(withSearch(editHref(bot, api.collection, other)));
        },
        close,
        isOpen: address.screen !== "list",
        listHistory,
        addHref: withSearch(newHref(bot, api.collection)),
        hrefOf: (view: View) => withSearch(editHref(bot, api.collection, idOf(view))),
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
            backHref: withSearch(listHref(bot, api.collection)),
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
                      },
            onDelete: () => (item === null ? Promise.resolve() : remove(item)),
            history,
        },
    };
}
