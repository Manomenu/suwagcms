import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";

import {
    createVersion,
    fetchStatus,
    fetchVersion,
    fetchVersions,
    numberOf,
    pushVersion,
    type BotStatus,
    type PushResult,
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

    if (failure !== null) {
        return (
            <main className="mx-auto max-w-2xl p-4">
                <Alert variant="destructive">{failure}</Alert>
            </main>
        );
    }
    if (versions === null) return <main className="mx-auto max-w-2xl p-4 text-muted-foreground">Loading versions…</main>;

    return (
        <VersionsView
            bot={bot}
            versions={versions}
            status={status}
            pushFailure={pushFailure}
            pushing={pushing}
            refused={refused}
            shown={shown}
            onPush={(number) => {
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
