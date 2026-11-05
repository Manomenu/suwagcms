import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";

import { deleteKnowledgeBase, fetchKnowledgeBases, revisionOf, type KnowledgeBase } from "./api";
import { emptyForm } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

type Open = { kind: "add" } | { kind: "rename"; base: KnowledgeBase } | null;

/** Loads the knowledge bases of the bot in the address when it opens, so bases added meanwhile from the CLI or another tab are found. */
export function KnowledgeBasesScreen({ bot }: { bot: string }) {
    const [bases, setBases] = useState<KnowledgeBase[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<Open>(null);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);

    const load = useCallback(
        (signal?: AbortSignal) => {
            const controller = new AbortController();
            fetchKnowledgeBases(bot, signal ?? controller.signal)
                .then(setBases)
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
        return <Alert variant="destructive">{failure}</Alert>;
    }
    if (bases === null) return <p className="text-muted-foreground">Loading knowledge bases…</p>;

    const renamedId = open?.kind === "rename" ? open.base.id : null;
    const latest = renamedId === null ? undefined : bases.find((base) => base.id === renamedId);

    function remove(base: KnowledgeBase) {
        setDeleteFailure(null);
        deleteKnowledgeBase(bot, base.id, revisionOf(base))
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
        <KnowledgeBasesView
            bot={bot}
            bases={bases}
            onAdd={() => {
                setOpen({ kind: "add" });
            }}
            onRename={(base) => {
                setOpen({ kind: "rename", base });
            }}
            onDelete={remove}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {open !== null && (
                        <KnowledgeBaseForm
                            key={open.kind === "rename" ? open.base.id : "add"}
                            bot={bot}
                            initial={open.kind === "rename" ? { id: open.base.id, title: open.base.title } : emptyForm()}
                            editing={open.kind === "rename"}
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
