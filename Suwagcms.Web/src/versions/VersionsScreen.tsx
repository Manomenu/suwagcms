import { useCallback, useEffect, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { ApiError } from "@/api/client";
import { useToFirstPage } from "@/api/pagination";
import { Alert } from "@/design/alert";

import {
    createVersion,
    fetchComparison,
    fetchPushPlan,
    fetchStatus,
    fetchVersion,
    fetchVersions,
    numberOf,
    pushVersion,
    type BotStatus,
    type PushPlan,
    type PushResult,
    type VersionComparison,
    type VersionDetail,
    type VersionSummary,
} from "./api";
import { VersionsView } from "./VersionsView";

/** Loads the versions of the bot in the address when it opens, so versions created meanwhile from the CLI are found. */
export function VersionsScreen({ bot }: { bot: string }) {
    const [versions, setVersions] = useState<VersionSummary[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [refused, setRefused] = useState<ApiError["mistakes"] | null>(null);
    const [shown, setShown] = useState<VersionDetail | null>(null);
    const [status, setStatus] = useState<BotStatus | null>(null);
    const [pushing, setPushing] = useState(false);
    const [pushFailure, setPushFailure] = useState<PushResult | null>(null);
    const [plan, setPlan] = useState<PushPlan | null>(null);
    const [comparison, setComparison] = useState<VersionComparison | null>(null);
    const [params] = useSearchParams();
    const [location, navigate] = useLocation();
    const fromParam = params.get("from");
    const toParam = params.get("to");
    const openParam = params.get("open");
    const toFirstPage = useToFirstPage();

    const compare = useCallback(
        (from: number, to: number) => {
            fetchComparison(bot, from, to)
                .then(setComparison)
                .catch((error: unknown) => {
                    setFailure(error instanceof Error ? error.message : String(error));
                });
        },
        [bot],
    );

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

    // Why: `suwagcms diff --open` arrives as ?from=<n>&to=<n>; it is taken once and dropped from the address.
    useEffect(() => {
        if (fromParam === null || toParam === null) return;
        navigate(location, { replace: true });
        compare(Number(fromParam), Number(toParam));
    }, [fromParam, toParam, location, navigate, compare]);

    // Why: `suwagcms export --open` arrives as ?open=<n>; it is taken once and dropped from the address.
    useEffect(() => {
        if (openParam === null) return;
        navigate(location, { replace: true });
        fetchVersion(bot, Number(openParam))
            .then(setShown)
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
            });
    }, [openParam, location, navigate, bot]);

    if (failure !== null) {
        return <Alert variant="destructive">{failure}</Alert>;
    }
    if (versions === null) return <p className="text-muted-foreground">Loading versions…</p>;

    return (
        <VersionsView
            bot={bot}
            versions={versions}
            status={status}
            pushFailure={pushFailure}
            pushing={pushing}
            refused={refused}
            shown={shown}
            comparison={comparison}
            onCompare={compare}
            onCloseComparison={() => {
                setComparison(null);
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
                        load();
                    });
            }}
            onCreate={() => {
                setRefused(null);
                createVersion(bot)
                    .then(() => {
                        // Why: the new version is the first row, and it is what the person came for.
                        toFirstPage();
                        load();
                    })
                    .catch((error: unknown) => {
                        if (error instanceof ApiError && error.mistakes.length > 0) setRefused(error.mistakes);
                        else setFailure(error instanceof Error ? error.message : String(error));
                    });
            }}
            onOpen={(version) => {
                fetchVersion(bot, numberOf(version))
                    .then(setShown)
                    .catch((error: unknown) => {
                        setFailure(error instanceof Error ? error.message : String(error));
                    });
            }}
            onClose={() => {
                setShown(null);
            }}
        />
    );
}
