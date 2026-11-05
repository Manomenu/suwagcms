import { useCallback, useEffect, useMemo, useState } from "react";

import { ApiError } from "@/api/client";
import { renamer } from "@/api/rename";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";

import { deleteKnowledgeBase, fetchKnowledgeBases, revisionOf, type KnowledgeBase } from "./api";
import { emptyForm } from "./knowledgeBaseForm";
import { KnowledgeBaseForm } from "./KnowledgeBaseForm";
import { KnowledgeBasesView } from "./KnowledgeBasesView";

type Open = { kind: "add" } | { kind: "edit"; base: KnowledgeBase } | null;

/** Loads the knowledge bases of the bot in the address when it opens, so bases added meanwhile from the CLI or another tab are found. */
export function KnowledgeBasesScreen({ bot }: { bot: string }) {
    const [bases, setBases] = useState<KnowledgeBase[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<Open>(null);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [changingId, setChangingId] = useState(false);
    const openId = open?.kind === "edit" ? open.base.id : "";
    const shown = bases?.find((base) => base.id === openId);
    const openRevision = shown === undefined ? 0 : revisionOf(shown);
    const changer = useMemo(() => renamer(bot, "knowledge-bases", openId, openRevision), [bot, openId, openRevision]);

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

    const editedId = open?.kind === "edit" ? open.base.id : null;
    const latest = editedId === null ? undefined : bases.find((base) => base.id === editedId);

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
            onEdit={(base) => {
                setOpen({ kind: "edit", base });
            }}
            onDelete={remove}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {open?.kind === "edit" && (
                        <div>
                            <Button
                                type="button"
                                variant="outline"
                                onClick={() => {
                                    setChangingId(true);
                                }}
                            >
                                Change id
                            </Button>
                        </div>
                    )}
                    {changingId && open?.kind === "edit" && (
                        <ChangeIdDialog
                            noun="knowledge base"
                            id={open.base.id}
                            preview={changer.preview}
                            apply={changer.apply}
                            onChanged={(id) => {
                                setChangingId(false);
                                void fetchKnowledgeBases(bot, new AbortController().signal).then((list) => {
                                    setBases(list);
                                    const base = list.find((other) => other.id === id);
                                    setOpen(base === undefined ? null : { kind: "edit", base });
                                });
                            }}
                            onCancel={() => {
                                setChangingId(false);
                            }}
                        />
                    )}
                    {open !== null && (
                        <KnowledgeBaseForm
                            key={open.kind === "edit" ? open.base.id : "add"}
                            bot={bot}
                            initial={
                                open.kind === "edit"
                                    ? { id: open.base.id, title: open.base.title, language: open.base.language }
                                    : emptyForm()
                            }
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
