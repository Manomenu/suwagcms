import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { ApiError } from "@/api/client";
import { useResourceHistory } from "@/api/history";
import { renamer } from "@/api/rename";
import { usageHref, useUsage } from "@/api/usage";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";
import { HistoryPanel } from "@/design/historyPanel";
import { UsagePanel } from "@/design/usagePanel";

import { emptyForm, formOf } from "./actionForm";
import { ActionForm } from "./ActionForm";
import { ActionsView } from "./ActionsView";
import { deleteAction, fetchAction, fetchActions, isCode, revisionOf, type ActionView } from "./api";
import { CodeActionPanel } from "./CodeActionPanel";
import { TestRunPanel } from "./TestRunPanel";

type Open = { kind: "add" } | { kind: "edit"; action: ActionView } | null;

/** Loads the actions of the bot in the address when it opens, so actions added meanwhile from the CLI or another tab are found. */
export function ActionsScreen({ bot }: { bot: string }) {
    const [actions, setActions] = useState<ActionView[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<Open>(null);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [location, navigate] = useLocation();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    const openAction = open?.kind === "edit" ? open.action : null;
    const editing = openAction !== null && !isCode(openAction);
    const openRevision = openAction === null ? 0 : revisionOf(actions?.find((action) => action.id === openAction.id) ?? openAction);
    const usage = useUsage(bot, "actions", openAction?.id ?? null, openRevision);
    const changer = useMemo(() => renamer(bot, "actions", openAction?.id ?? "", openRevision), [bot, openAction?.id, openRevision]);
    const history = useResourceHistory(bot, "actions", openAction?.id ?? null, openAction === null ? 0 : revisionOf(openAction));

    const load = useCallback(
        (signal?: AbortSignal) => {
            const controller = new AbortController();
            fetchActions(bot, signal ?? controller.signal)
                .then(setActions)
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

    // Why: a link from another screen arrives as ?open=<id>; it is taken once and dropped from the address.
    useEffect(() => {
        if (openParam === null) return;
        navigate(location, { replace: true });
        fetchAction(bot, openParam, new AbortController().signal)
            .then((action) => {
                setOpen({ kind: "edit", action });
            })
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
            });
    }, [openParam, location, navigate, bot]);

    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (actions === null) return <p className="text-muted-foreground">Loading actions…</p>;

    const latest = openAction === null ? undefined : actions.find((action) => action.id === openAction.id);

    function remove(action: ActionView) {
        setDeleteFailure(null);
        deleteAction(bot, action.id, revisionOf(action))
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
        <ActionsView
            bot={bot}
            actions={actions}
            onAdd={() => {
                setOpen({ kind: "add" });
            }}
            onOpen={(action) => {
                setOpen({ kind: "edit", action });
            }}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {editing && (
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
                    {changingId && openAction !== null && (
                        <ChangeIdDialog
                            noun="action"
                            id={openAction.id}
                            preview={changer.preview}
                            apply={changer.apply}
                            onChanged={(id) => {
                                setChangingId(false);
                                setOpen(null);
                                load();
                                navigate(`${location}?open=${encodeURIComponent(id)}`);
                            }}
                            onCancel={() => {
                                setChangingId(false);
                            }}
                        />
                    )}
                    {openAction !== null && (
                        <UsagePanel
                            usage={usage.usage}
                            failure={usage.failure}
                            hrefOf={(entry) => usageHref(bot, entry)}
                            {...(editing
                                ? {
                                      onDelete: () => {
                                          remove(openAction);
                                      },
                                  }
                                : {})}
                        />
                    )}
                    {openAction !== null && isCode(openAction) && (
                        <CodeActionPanel
                            action={openAction}
                            onClose={() => {
                                setOpen(null);
                            }}
                        />
                    )}
                    {open !== null && (open.kind === "add" || editing) && (
                        <ActionForm
                            key={openAction?.id ?? "add"}
                            bot={bot}
                            initial={openAction === null ? emptyForm() : formOf(openAction)}
                            editing={openAction !== null}
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
                            {...(openAction === null
                                ? {}
                                : { testRun: (unsaved: boolean) => <TestRunPanel bot={bot} action={openAction} unsaved={unsaved} /> })}
                        />
                    )}
                    {openAction !== null && isCode(openAction) && <TestRunPanel bot={bot} action={openAction} unsaved={false} />}
                    {openAction !== null && <HistoryPanel {...history} />}
                </>
            }
        />
    );
}
