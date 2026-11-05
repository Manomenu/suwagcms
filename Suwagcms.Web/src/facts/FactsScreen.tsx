import { useCallback, useEffect, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { ApiError } from "@/api/client";
import { usageHref, useUsage } from "@/api/usage";
import { Alert } from "@/design/alert";
import { UsagePanel } from "@/design/usagePanel";

import { deleteFact, fetchFact, fetchFacts, revisionOf, type Fact } from "./api";
import { emptyForm, formOf } from "./factForm";
import { FactForm } from "./FactForm";
import { FactsView } from "./FactsView";

type Open = { kind: "add" } | { kind: "edit"; fact: Fact } | null;

/** Loads the facts of the bot in the address when it opens, so facts added meanwhile from the CLI or another tab are found. */
export function FactsScreen({ bot }: { bot: string }) {
    const [facts, setFacts] = useState<Fact[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<Open>(null);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [location, navigate] = useLocation();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    const usage = useUsage(bot, "facts", open?.kind === "edit" ? open.fact.name : null);

    const load = useCallback(
        (signal?: AbortSignal) => {
            const controller = new AbortController();
            fetchFacts(bot, signal ?? controller.signal)
                .then(setFacts)
                .catch((error: unknown) => {
                    if (!(signal ?? controller.signal).aborted) setFailure(error instanceof Error ? error.message : String(error));
                });
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

    // Why: a link from the Used by panel of another screen arrives as ?open=<name>; it is taken once and dropped from the address.
    useEffect(() => {
        if (openParam === null) return;
        navigate(location, { replace: true });
        fetchFact(bot, openParam, new AbortController().signal)
            .then((fact) => {
                setOpen({ kind: "edit", fact });
            })
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
            });
    }, [openParam, location, navigate, bot]);

    if (failure !== null) {
        return <Alert variant="destructive">{failure}</Alert>;
    }
    if (facts === null) return <p className="text-muted-foreground">Loading facts…</p>;

    const editedName = open?.kind === "edit" ? open.fact.name : null;
    const latest = editedName === null ? undefined : facts.find((fact) => fact.name === editedName);

    function remove(fact: Fact) {
        setDeleteFailure(null);
        deleteFact(bot, fact.name, revisionOf(fact))
            .then(() => {
                setOpen(null);
                load();
            })
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...error.mistakes.map((m) => `${m.message} ${m.fix}`)].join(" ")
                        : String(error),
                );
            });
    }

    return (
        <FactsView
            bot={bot}
            facts={facts}
            onAdd={() => {
                setOpen({ kind: "add" });
            }}
            onEdit={(fact) => {
                setOpen({ kind: "edit", fact });
            }}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {open?.kind === "edit" && (
                        <UsagePanel
                            usage={usage.usage}
                            failure={usage.failure}
                            hrefOf={(entry) => usageHref(bot, entry)}
                            onDelete={() => {
                                remove(open.fact);
                            }}
                        />
                    )}
                    {open !== null && (
                        <FactForm
                            key={open.kind === "edit" ? open.fact.name : "add"}
                            bot={bot}
                            initial={open.kind === "edit" ? formOf(open.fact) : emptyForm()}
                            editing={open.kind === "edit"}
                            revision={latest === undefined ? 0 : revisionOf(latest)}
                            onSaved={() => {
                                setOpen(null);
                                load();
                            }}
                            onReload={() => {
                                load();
                            }}
                            onCancel={() => {
                                setOpen(null);
                            }}
                        />
                    )}
                </>
            }
        />
    );
}
