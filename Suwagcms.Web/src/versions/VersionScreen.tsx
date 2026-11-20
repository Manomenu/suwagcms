import { useEffect, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { ApiError } from "@/api/client";
import { useVersionWindow } from "@/api/history";
import { versionsHref, withListSearch } from "@/api/resourceAddress";
import { Alert } from "@/design/alert";

import { fetchComparison, fetchVersion, fetchVersions, versionNumberOf, type VersionDetail, type VersionSummary } from "./api";
import { NoSuchVersion, VersionView, type ComparisonState } from "./VersionView";

const COMPARE = "compare";

function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}

export function VersionScreen({ bot, asked }: { bot: string; asked: string }) {
    const number = versionNumberOf(asked);
    const [detail, setDetail] = useState<VersionDetail | null>(null);
    const [versions, setVersions] = useState<VersionSummary[]>([]);
    const [missing, setMissing] = useState(number === null);
    const [failure, setFailure] = useState<string | null>(null);
    const [comparison, setComparison] = useState<ComparisonState | null>(null);
    const history = useVersionWindow(bot, number);
    const search = useSearch();
    const [params, setParams] = useSearchParams();
    const [, navigate] = useLocation();
    const compared = versionNumberOf(params.get(COMPARE));

    useEffect(() => {
        if (number === null) return undefined;
        const controller = new AbortController();
        const fail = (error: unknown) => {
            if (controller.signal.aborted) return;
            if (error instanceof ApiError && error.status === 404) setMissing(true);
            else setFailure(messageOf(error));
        };
        fetchVersion(bot, number, controller.signal).then(setDetail).catch(fail);
        fetchVersions(bot, controller.signal).then(setVersions).catch(fail);
        return () => {
            controller.abort();
        };
    }, [bot, number]);

    useEffect(() => {
        if (number === null || compared === null) return undefined;
        const controller = new AbortController();
        fetchComparison(bot, compared, number, controller.signal)
            .then((answer) => {
                setComparison({ with: compared, answer, refusal: null });
            })
            .catch((error: unknown) => {
                if (controller.signal.aborted) return;
                const refusal = error instanceof ApiError ? (error.mistakes[0]?.message ?? error.message) : messageOf(error);
                setComparison({ with: compared, answer: null, refusal });
            });
        return () => {
            controller.abort();
        };
    }, [bot, number, compared]);

    const toList = () => {
        navigate(withListSearch(versionsHref(bot), search));
    };
    const writeCompared = (other: number | null) => {
        setParams(
            (was) => {
                const next = new URLSearchParams(was);
                if (other === null) next.delete(COMPARE);
                else next.set(COMPARE, String(other));
                return next;
            },
            { replace: true },
        );
    };

    if (missing) return <NoSuchVersion asked={asked} onBack={toList} />;
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (detail === null) return <p className="text-muted-foreground">Loading the version…</p>;
    return (
        <VersionView
            bot={bot}
            detail={detail}
            versions={versions}
            comparison={
                compared === null ? null : comparison?.with === compared ? comparison : { with: compared, answer: null, refusal: null }
            }
            history={history}
            onCompare={writeCompared}
            onCloseComparison={() => {
                writeCompared(null);
            }}
            onBack={toList}
        />
    );
}
