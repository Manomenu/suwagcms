import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";

import { deleteFact, fetchFacts, revisionOf, type Fact } from "./api";
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

    if (failure !== null) {
        return (
            <main className="mx-auto max-w-2xl p-4">
                <Alert variant="destructive">{failure}</Alert>
            </main>
        );
    }
    if (facts === null) return <main className="mx-auto max-w-2xl p-4 text-muted-foreground">Loading facts…</main>;

    const editedName = open?.kind === "edit" ? open.fact.name : null;
    const latest = editedName === null ? undefined : facts.find((fact) => fact.name === editedName);

    function remove(fact: Fact) {
        setDeleteFailure(null);
        deleteFact(bot, fact.name, revisionOf(fact))
            .then(() => {
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
            onDelete={remove}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
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
