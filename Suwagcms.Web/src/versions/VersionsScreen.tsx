import { useCallback, useEffect, useState } from "react";
import { Redirect, useLocation, useSearch, useSearchParams } from "wouter";

import { ApiError } from "@/api/client";
import { useKindHistory } from "@/api/history";
import { useToFirstPage } from "@/api/pagination";
import { versionHref, versionsHref, withListSearch } from "@/api/resourceAddress";
import { Alert } from "@/design/alert";

import {
    createVersion,
    fetchPushPlan,
    fetchStatus,
    fetchVersions,
    numberOf,
    pushVersion,
    versionNumberOf,
    type BotStatus,
    type PushPlan,
    type PushResult,
    type VersionSummary,
} from "./api";
import { VersionsView } from "./VersionsView";

/**
 * Where an address of an earlier kind now leads, null when it is of today's: `suwagcms export --open` arrives as ?open=<n> and
 * `suwagcms diff --open` as ?from=<n>&to=<n>, and a link kept from before them still works.
 */
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

/** Loads the versions of the bot in the address when it opens, so versions created meanwhile from the CLI are found. */
export function VersionsScreen({ bot }: { bot: string }) {
    const [versions, setVersions] = useState<VersionSummary[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [refused, setRefused] = useState<ApiError["mistakes"] | null>(null);
    const [status, setStatus] = useState<BotStatus | null>(null);
    const [pushing, setPushing] = useState(false);
    const [pushFailure, setPushFailure] = useState<PushResult | null>(null);
    const [plan, setPlan] = useState<PushPlan | null>(null);
    const [params] = useSearchParams();
    const search = useSearch();
    const [, navigate] = useLocation();
    const toFirstPage = useToFirstPage();
    const earlier = earlierAddress(bot, params);
    // Why: creating and pushing add a version event, which is on the panel's newest page; 0 would read as "not loaded yet".
    const [events, setEvents] = useState(1);
    const history = useKindHistory(bot, earlier === null ? "versions" : null, events);

    const load = useCallback(
        (signal?: AbortSignal) => {
            const controller = new AbortController();
            const used = signal ?? controller.signal;
            const fail = (error: unknown) => {
                if (!used.aborted) setFailure(error instanceof Error ? error.message : String(error));
            };
            fetchVersions(bot, used).then(setVersions).catch(fail);
            // The bot is asked every time the page loads: what it has is its own answer, never a remembered one.
            fetchStatus(bot, used).then(setStatus).catch(fail);
        },
        [bot],
    );

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        load(controller.signal);
        return () => {
            controller.abort();
        };
    }, [load]);

    if (earlier !== null) return <Redirect to={earlier} replace />;
    if (failure !== null) {
        return <Alert variant="destructive">{failure}</Alert>;
    }
    if (versions === null) return <p className="text-muted-foreground">Loading versions…</p>;

    return (
        <VersionsView
            bot={bot}
            versions={versions}
            history={history}
            status={status}
            pushFailure={pushFailure}
            pushing={pushing}
            refused={refused}
            onCompare={(from, to) => {
                navigate(`${versionHref(bot, to)}?compare=${from}`);
            }}
            plan={plan}
            onPlan={(number) => {
                fetchPushPlan(bot, number)
                    .then(setPlan)
                    .catch((error: unknown) => {
                        setFailure(error instanceof Error ? error.message : String(error));
                    });
            }}
            onCancelPlan={() => {
                setPlan(null);
            }}
            onPush={(number) => {
                setPlan(null);
                setPushing(true);
                setPushFailure(null);
                pushVersion(bot, number)
                    .then((result) => {
                        if (result.outcome === "Failed") setPushFailure(result);
                    })
                    .catch((error: unknown) => {
                        setFailure(error instanceof Error ? error.message : String(error));
                    })
                    .finally(() => {
                        setPushing(false);
                        setEvents((was) => was + 1);
                        load();
                    });
            }}
            onCreate={() => {
                setRefused(null);
                createVersion(bot)
                    .then(() => {
                        // Why: the new version is the first row, and it is what the person came for.
                        toFirstPage();
                        setEvents((was) => was + 1);
                        load();
                    })
                    .catch((error: unknown) => {
                        if (error instanceof ApiError && error.mistakes.length > 0) setRefused(error.mistakes);
                        else setFailure(error instanceof Error ? error.message : String(error));
                    });
            }}
            onOpen={(version) => {
                navigate(withListSearch(versionHref(bot, numberOf(version)), search));
            }}
        />
    );
}
