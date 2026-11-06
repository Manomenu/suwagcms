import { useEffect, useMemo, useReducer, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { useAvailability } from "@/api/availability";
import { ApiError } from "@/api/client";
import { fetchDeclaration } from "@/api/declaration";
import { useResourceHistory } from "@/api/history";
import { mayLeave, useLeaveGuard } from "@/api/leaveGuard";
import { renamer } from "@/api/rename";
import { editHref, listHref, withListSearch } from "@/api/resourceAddress";
import { usageHref, useUsage, type UsageLink } from "@/api/usage";
import { Alert } from "@/design/alert";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { Button } from "@/design/button";
import { MistakeItems } from "@/design/mistakeItems";
import { BackToList, ResourceEdit } from "@/design/resourceEdit";

import {
    deleteProcedure,
    fetchActions,
    fetchArticles,
    fetchFacts,
    fetchKnowledgeBases,
    fetchProcedure,
    fetchProcedures,
    saveProcedure,
    type ActionView,
    type Fact,
    type ProcedureDetail,
} from "./api";
import { toSave, type LinkableArticle, type LinkableBase } from "./draft";
import { offeredBlocks, type OfferedBlocks } from "./offeredBlocks";
import { ProcedureForm } from "./ProcedureForm";
import { isDirty, loaded as stateOf, outcomeOf, reduce } from "./state";
import { useLiveCheck } from "./useLiveCheck";

interface Loaded {
    detail: ProcedureDetail;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    bases: LinkableBase[];
    actions: ActionView[];
    offered: OfferedBlocks;
}

// Why: every conversation is based on this procedure, so the server refuses to rename it, delete it or turn it off.
const BASE_PROCEDURE = "conversation";

function Body({ bot, id, initial, onBack }: { bot: string; id: string; initial: Loaded; onBack: () => void }) {
    const [state, dispatch] = useReducer(reduce, initial.detail, stateOf);
    const [params, setParams] = useSearchParams();
    // Why: the selected step is in the address, so a reload or a sent link opens the same step.
    const selected = params.get("step");
    const setSelected = (step: string | null) => {
        setParams(
            (was) => {
                const next = new URLSearchParams(was);
                if (step === null) next.delete("step");
                else next.set("step", step);
                return next;
            },
            { replace: true },
        );
    };
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const [changingId, setChangingId] = useState(false);
    const [, navigate] = useLocation();
    const { draft } = state;
    const check = useLiveCheck(bot, id, state.revision, draft);
    const usage = useUsage(bot, "procedures", id, state.revision);
    const history = useResourceHistory(bot, "procedures", id, state.revision);
    const availability = useAvailability(bot);
    const dirty = isDirty(state);
    const changer = useMemo(() => renamer(bot, "procedures", id, state.revision), [bot, id, state.revision]);
    const isBase = id === BASE_PROCEDURE;

    useLeaveGuard(dirty, "You have unsaved changes. Leave and discard them?");

    const save = () => {
        setSaving(true);
        const body = toSave(draft);
        const sent = JSON.stringify(body);
        void outcomeOf(saveProcedure(bot, id, body, state.revision, false))
            .then((outcome) => {
                dispatch({ type: "saved", outcome, sent });
            })
            .finally(() => {
                setSaving(false);
            });
    };

    const remove = () => {
        setDeleteFailure(null);
        deleteProcedure(bot, id, state.revision)
            .then(() => {
                navigate(withListSearch(listHref(bot, "procedures"), params.toString()), { replace: true });
            })
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...error.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`)].join(" ")
                        : String(error),
                );
            });
    };

    const reload = () => {
        void fetchProcedure(bot, id, new AbortController().signal).then((detail) => {
            dispatch({ type: "loaded", detail });
            setSelected(null);
        });
    };

    const stale = (message: string) => (
        <Alert variant="destructive" className="flex items-center gap-3">
            <span className="flex-1">Not saved: {message} Your page is still here; reloading replaces it with what was saved.</span>
            <Button type="button" size="sm" variant="outline" onClick={reload}>
                Reload
            </Button>
        </Alert>
    );
    const notice = state.notice;
    return (
        <ResourceEdit
            noun="procedure"
            plural="procedures"
            addTitle="Add a procedure"
            onBack={onBack}
            ready
            missingId={null}
            editedId={id}
            editable={!isBase}
            deleteFailure={deleteFailure}
            dirty={dirty}
            changingId={changingId}
            onChangingId={setChangingId}
            preview={changer.preview}
            apply={changer.apply}
            onChanged={(newId) => {
                setChangingId(false);
                navigate(withListSearch(editHref(bot, "procedures", newId), params.toString()), { replace: true });
            }}
            usage={{
                usage: usage.usage,
                failure: usage.failure,
                hrefOf: (entry: UsageLink) => usageHref(bot, entry),
                onFollow: (event: React.MouseEvent) => {
                    if (!mayLeave()) event.preventDefault();
                },
            }}
            onDelete={remove}
            history={history}
            notice={
                <>
                    <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />
                    {notice?.kind === "stale" && stale(notice.message)}
                    {notice?.kind === "refused" && (
                        <Alert variant="destructive">
                            <p>
                                Not saved: fix {notice.mistakes.length === 1 ? "this mistake" : "these mistakes"} first. Your page is
                                unchanged.
                            </p>
                            <MistakeItems mistakes={notice.mistakes} />
                        </Alert>
                    )}
                    {notice?.kind === "failed" && <Alert variant="destructive">Not saved: {notice.message}</Alert>}
                    {check.state === "stale" && notice === null && stale(check.message)}
                </>
            }
            panel={
                !isBase && (
                    <AvailabilityPanel
                        id={id}
                        off={availability.isOff("procedure", id)}
                        delivery={availability.delivery}
                        usedBy={usage.usage?.usedBy ?? null}
                        onTurn={(off) => {
                            availability.turn("procedure", id, off);
                        }}
                    />
                )
            }
        >
            <ProcedureForm
                bot={bot}
                id={id}
                draft={draft}
                allFacts={initial.facts}
                procedures={initial.procedures}
                offered={initial.offered.blocks}
                note={initial.offered.note}
                showKnowledge={initial.offered.knowledge}
                bases={initial.bases}
                articles={initial.articles}
                actions={initial.actions}
                selected={selected}
                check={check}
                dirty={dirty}
                saving={saving}
                canBeInterrupted={!isBase}
                edit={(change) => {
                    dispatch({ type: "edited", draft: change(draft) });
                }}
                onSelect={setSelected}
                onSave={save}
                onLeave={(href) => {
                    if (mayLeave()) navigate(href);
                }}
            />
        </ResourceEdit>
    );
}

/** The edit page of one procedure: everything the form offers is read when it opens, so what was saved meanwhile is found. */
export function ProcedureScreen({ bot, id }: { bot: string; id: string }) {
    const [loaded, setLoaded] = useState<Loaded | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [missing, setMissing] = useState(false);
    const [, navigate] = useLocation();
    const search = useSearch();

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        Promise.all([
            fetchProcedure(bot, id, controller.signal),
            fetchFacts(bot, controller.signal),
            fetchProcedures(bot, controller.signal),
            fetchArticles(bot, controller.signal),
            fetchKnowledgeBases(bot, controller.signal),
            // Why: the last answer, not a new ask: opening a form must not wait on a bot.
            fetchDeclaration(bot, controller.signal, false),
            fetchActions(bot, controller.signal),
        ])
            .then(([detail, facts, list, articles, bases, declaration, actions]) => {
                setLoaded({
                    detail,
                    facts,
                    procedures: list.procedures.map((procedure) => procedure.id),
                    articles: articles.map((article) => ({ id: article.id, title: article.title, knowledgeBase: article.knowledgeBase })),
                    bases: bases.map((base) => ({ id: base.id, title: base.title })),
                    actions,
                    offered: offeredBlocks(declaration),
                });
            })
            .catch((error: unknown) => {
                if (controller.signal.aborted) return;
                if (error instanceof ApiError && error.status === 404) setMissing(true);
                else setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot, id]);

    const onBack = () => {
        if (mayLeave()) navigate(withListSearch(listHref(bot, "procedures"), search));
    };
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (missing) {
        return (
            <div className="flex flex-col gap-4">
                <BackToList plural="procedures" onBack={onBack} />
                <Alert variant="destructive">This bot has no procedure '{id}'. It may have been deleted, or its id changed.</Alert>
            </div>
        );
    }
    if (loaded === null) return <p className="text-muted-foreground">Loading {id}…</p>;
    return <Body bot={bot} id={id} initial={loaded} onBack={onBack} />;
}
