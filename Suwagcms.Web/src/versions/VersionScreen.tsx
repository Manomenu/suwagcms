import { Alert, ApiError, Spinner } from "@maniumek/suwfrontkit";
import { messageOf, mistakesOf } from "@/api/client";
import { skipToken, useQuery } from "@tanstack/react-query";
import { useSearch, useSearchParams } from "wouter";

import { useVersionWindow } from "@/api/history";
import { versionsHref, withListSearch } from "@/api/resourceAddress";

import { fetchComparison, fetchVersion, fetchVersions, versionNumberOf, versionsKey } from "./api";
import { NoSuchVersion, VersionView, type ComparisonState } from "./VersionView";

const COMPARE = "compare";

function refusalOf(error: unknown): string {
    return error instanceof ApiError ? (mistakesOf(error)[0]?.message ?? error.message) : messageOf(error);
}

function isMissing(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404;
}

export function VersionScreen({ bot, asked }: { bot: string; asked: string }) {
    const number = versionNumberOf(asked);
    const history = useVersionWindow(bot, number);
    const search = useSearch();
    const [params, setParams] = useSearchParams();
    const compared = versionNumberOf(params.get(COMPARE));
    const detail = useQuery({
        queryKey: versionsKey(bot, "item", number),
        queryFn: number === null ? skipToken : ({ signal }) => fetchVersion(bot, number, signal),
    });
    const versions = useQuery({
        queryKey: versionsKey(bot, "list"),
        queryFn: number === null ? skipToken : ({ signal }) => fetchVersions(bot, signal),
    });
    const comparison = useQuery({
        queryKey: versionsKey(bot, "diff", compared, number),
        queryFn:
            number === null || compared === null || detail.data === undefined
                ? skipToken
                : ({ signal }) => fetchComparison(bot, compared, number, signal),
    });
    const missing = number === null || isMissing(detail.error) || isMissing(versions.error);
    const failed = detail.error ?? versions.error;
    const compareState: ComparisonState | null =
        compared === null
            ? null
            : {
                  with: compared,
                  answer: comparison.data ?? null,
                  refusal: comparison.error === null ? null : refusalOf(comparison.error),
              };

    const listAddress = withListSearch(versionsHref(bot), search);
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

    if (missing) return <NoSuchVersion asked={asked} backHref={listAddress} />;
    if (failed !== null) return <Alert variant="destructive">{messageOf(failed)}</Alert>;
    if (detail.data === undefined) return <Spinner label="Loading the version…" size="sm" />;
    return (
        <VersionView
            bot={bot}
            detail={detail.data}
            versions={versions.data ?? []}
            comparison={compareState}
            history={history}
            onCompare={writeCompared}
            onCloseComparison={() => {
                writeCompared(null);
            }}
            backHref={listAddress}
        />
    );
}
