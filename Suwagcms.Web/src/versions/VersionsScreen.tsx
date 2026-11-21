import { Alert, ApiError, Spinner } from "@maniumek/suwfrontkit";
import { messageOf, mistakesOf } from "@/api/client";
import { skipToken, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Redirect, useLocation, useSearch, useSearchParams } from "wouter";

import { useKindHistory } from "@/api/history";
import { useToFirstPage } from "@/api/pagination";
import { versionHref, versionsHref, withListSearch } from "@/api/resourceAddress";

import { createVersion, fetchPushPlan, fetchStatus, fetchVersions, numberOf, pushVersion, versionNumberOf, versionsKey } from "./api";
import { VersionsView } from "./VersionsView";

function earlierAddress(bot: string, params: URLSearchParams): string | null {
    const open = params.get("open");
    const from = params.get("from");
    const to = params.get("to");
    if (open === null && from === null && to === null) return null;
    const opened = versionNumberOf(open);
    if (opened !== null) return versionHref(bot, opened);
    const later = versionNumberOf(to);
    const earlier = versionNumberOf(from);
    if (later !== null && earlier !== null) return `${versionHref(bot, later)}?compare=${earlier}`;
    const rest = new URLSearchParams(params);
    for (const name of ["open", "from", "to"]) rest.delete(name);
    return withListSearch(versionsHref(bot), rest.toString());
}

export function VersionsScreen({ bot }: { bot: string }) {
    const [planned, setPlanned] = useState<number | null>(null);
    const [params] = useSearchParams();
    const search = useSearch();
    const [, navigate] = useLocation();
    const toFirstPage = useToFirstPage();
    const earlier = earlierAddress(bot, params);
    const [events, setEvents] = useState(1);
    const history = useKindHistory(bot, earlier === null ? "versions" : null, events);
    const queries = useQueryClient();
    const versions = useQuery({ queryKey: versionsKey(bot, "list"), queryFn: ({ signal }) => fetchVersions(bot, signal) });
    const status = useQuery({ queryKey: versionsKey(bot, "status"), queryFn: ({ signal }) => fetchStatus(bot, signal) });
    const plan = useQuery({
        queryKey: versionsKey(bot, "plan", planned),
        queryFn: planned === null ? skipToken : ({ signal }) => fetchPushPlan(bot, planned, signal),
        gcTime: 0,
    });
    const changed = () => {
        setEvents((was) => was + 1);
        void queries.invalidateQueries({ queryKey: versionsKey(bot) });
    };
    const push = useMutation({ mutationFn: (number: number) => pushVersion(bot, number), onSettled: changed });
    const create = useMutation({
        mutationFn: () => createVersion(bot),
        onSuccess: () => {
            toFirstPage();
            changed();
        },
    });
    const refused = create.error instanceof ApiError && mistakesOf(create.error).length > 0 ? mistakesOf(create.error) : null;
    const failed = versions.error ?? status.error ?? plan.error ?? push.error ?? (refused === null ? create.error : null);

    if (earlier !== null) return <Redirect to={earlier} replace />;
    if (failed !== null) {
        return <Alert variant="destructive">{messageOf(failed)}</Alert>;
    }
    if (versions.data === undefined) return <Spinner label="Loading versions…" size="sm" />;

    return (
        <VersionsView
            bot={bot}
            versions={versions.data}
            history={history}
            status={status.data ?? null}
            pushFailure={push.data?.outcome === "Failed" ? push.data : null}
            pushing={push.isPending}
            refused={refused}
            onCompare={(from, to) => {
                navigate(`${versionHref(bot, to)}?compare=${from}`);
            }}
            plan={planned === null ? null : (plan.data ?? null)}
            onPlan={setPlanned}
            onCancelPlan={() => {
                setPlanned(null);
            }}
            onPush={(number) => {
                setPlanned(null);
                push.mutate(number);
            }}
            onCreate={() => {
                create.mutate();
            }}
            onOpen={(version) => {
                navigate(withListSearch(versionHref(bot, numberOf(version)), search));
            }}
        />
    );
}
