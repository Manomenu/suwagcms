import { Alert, ApiError, Button, MistakeList, Spinner } from "@maniumek/suwfrontkit";
import { messageOf, mistakesOf } from "@/api/client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useReducer, useState } from "react";
import { useLocation, useSearch, useSearchParams } from "wouter";

import { useAvailability } from "@/api/availability";
import { fetchDeclaration } from "@/api/declaration";
import { useResourceHistory } from "@/api/history";
import { useLeaveGuard } from "@maniumek/suwfrontkit";
import { renamer } from "@/api/rename";
import { editHref, listHref, withListSearch } from "@/api/resourceAddress";
import { usageLinkOf, useUsage } from "@/api/usage";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { BackToList, ResourceEdit } from "@/design/resourceEdit";

import {
    deleteProcedure,
    fetchActions,
    fetchArticles,
    fetchFacts,
    fetchKnowledgeBases,
    fetchProcedure,
    fetchProcedures,
    procedureKey,
    proceduresKey,
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

const BASE_PROCEDURE = "conversation";

function Body({
    bot,
    id,
    initial,
    backHref,
    reloaded,
}: {
    bot: string;
    id: string;
    initial: Loaded;
    backHref: string;
    reloaded: () => Promise<ProcedureDetail | undefined>;
}) {
    const [state, dispatch] = useReducer(reduce, initial.detail, stateOf);
    const queries = useQueryClient();
    const [params, setParams] = useSearchParams();
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
    const openConditions = params.getAll("if");
    const setConditionOpen = (row: string, open: boolean) => {
        setParams(
            (was) => {
                const next = new URLSearchParams(was);
                next.delete("if");
                for (const other of was.getAll("if")) if (other !== row) next.append("if", other);
                if (open) next.append("if", row);
                return next;
            },
            { replace: true },
        );
    };
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
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

    const saving = useMutation({
        mutationFn: (body: ReturnType<typeof toSave>) => outcomeOf(saveProcedure(bot, id, body, state.revision, false)),
        onSuccess: (outcome, body) => {
            dispatch({ type: "saved", outcome, sent: JSON.stringify(body) });
            if (outcome.kind === "saved") void queries.invalidateQueries({ queryKey: proceduresKey(bot, "list") });
        },
    });
    const save = () => {
        saving.mutate(toSave(draft));
    };

    const remove = () => {
        setDeleteFailure(null);
        return deleteProcedure(bot, id, state.revision)
            .then(() => {
                navigate(withListSearch(listHref(bot, "procedures"), params.toString()), { replace: true });
                void queries.invalidateQueries({ queryKey: proceduresKey(bot, "list") });
            })
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...mistakesOf(error).map((mistake) => `${mistake.message} ${mistake.fix}`)].join(" ")
                        : String(error),
                );
            });
    };

    const reload = () => {
        void reloaded().then((detail) => {
            if (detail === undefined) return;
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
            backHref={backHref}
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
                void queries.invalidateQueries({ queryKey: proceduresKey(bot, "list") });
                void queries.invalidateQueries({ queryKey: ["resources", bot] });
            }}
            usage={{
                usage: usage.usage,
                failure: usage.failure,
                linkOf: usageLinkOf(bot),
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
                            <MistakeList mistakes={notice.mistakes} />
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
                saving={saving.isPending}
                canBeInterrupted={!isBase}
                edit={(change) => {
                    dispatch({ type: "edited", draft: change(draft) });
                }}
                onSelect={setSelected}
                openConditions={openConditions}
                onConditionOpen={setConditionOpen}
                onSave={save}
            />
        </ResourceEdit>
    );
}

function useLoaded(bot: string, id: string) {
    const detail = useQuery({ queryKey: procedureKey(bot, id), queryFn: ({ signal }) => fetchProcedure(bot, id, signal), gcTime: 0 });
    const list = useQuery({ queryKey: proceduresKey(bot, "list"), queryFn: ({ signal }) => fetchProcedures(bot, signal) });
    const facts = useQuery({ queryKey: ["resources", bot, "facts", "list", null], queryFn: ({ signal }) => fetchFacts(bot, signal) });
    const articles = useQuery({
        queryKey: ["resources", bot, "articles", "list", null],
        queryFn: ({ signal }) => fetchArticles(bot, signal),
    });
    const bases = useQuery({
        queryKey: ["resources", bot, "knowledge-bases", "list", null],
        queryFn: ({ signal }) => fetchKnowledgeBases(bot, signal),
    });
    const declaration = useQuery({
        queryKey: ["declaration", bot, "stored"],
        queryFn: ({ signal }) => fetchDeclaration(bot, signal, false),
    });
    const actions = useQuery({ queryKey: ["resources", bot, "actions", "list", null], queryFn: ({ signal }) => fetchActions(bot, signal) });
    const error =
        [detail, list, facts, articles, bases, declaration, actions].find((query) => query.data === undefined && query.error !== null)
            ?.error ?? null;
    const loaded: Loaded | null =
        detail.data === undefined ||
        list.data === undefined ||
        facts.data === undefined ||
        articles.data === undefined ||
        bases.data === undefined ||
        declaration.data === undefined ||
        actions.data === undefined
            ? null
            : {
                  detail: detail.data,
                  facts: facts.data,
                  procedures: list.data.procedures.map((procedure) => procedure.id),
                  articles: articles.data.map((article) => ({
                      id: article.id,
                      title: article.title,
                      knowledgeBase: article.knowledgeBase,
                  })),
                  bases: bases.data.map((base) => ({ id: base.id, title: base.title })),
                  actions: actions.data,
                  offered: offeredBlocks(declaration.data),
              };
    return {
        loaded,
        error,
        reloaded: () => detail.refetch().then((again) => (again.error === null ? again.data : undefined)),
    };
}

export function ProcedureScreen({ bot, id }: { bot: string; id: string }) {
    const { loaded, error, reloaded } = useLoaded(bot, id);
    const search = useSearch();

    const backHref = withListSearch(listHref(bot, "procedures"), search);
    if (error instanceof ApiError && error.status === 404) {
        return (
            <div className="flex flex-col gap-4">
                <BackToList plural="procedures" backHref={backHref} />
                <Alert variant="destructive">This bot has no procedure '{id}'. It may have been deleted, or its id changed.</Alert>
            </div>
        );
    }
    if (error !== null) return <Alert variant="destructive">{messageOf(error)}</Alert>;
    if (loaded === null) return <Spinner label={`Loading ${id}…`} size="sm" />;
    return <Body key={id} bot={bot} id={id} initial={loaded} backHref={backHref} reloaded={reloaded} />;
}
