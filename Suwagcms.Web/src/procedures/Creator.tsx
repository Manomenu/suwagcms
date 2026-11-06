import "@xyflow/react/dist/style.css";

import {
    Background,
    Controls,
    MarkerType,
    ReactFlow,
    ReactFlowProvider,
    useReactFlow,
    type Connection,
    type Edge as FlowEdge,
    type NodeChange,
} from "@xyflow/react";
import { useEffect, useMemo, useReducer, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { renamer } from "@/api/rename";
import { ApiError } from "@/api/client";
import { editHref, listHref } from "@/api/resourceAddress";
import { useAvailability } from "@/api/availability";
import { useResourceHistory } from "@/api/history";
import { useUsage } from "@/api/usage";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";
import { InfoTip } from "@/design/infoTip";
import { changeIdHelp } from "@/design/resourceEdit";
import { mayLeave, useLeaveGuard } from "@/api/leaveGuard";
import { MistakeItems } from "@/design/mistakeItems";

import { type ActionView, deleteProcedure, fetchProcedure, saveLayout, saveProcedure, type Fact, type ProcedureDetail } from "./api";
import {
    addStep,
    connect,
    deleteStep,
    disconnect,
    edgesOf,
    factsSeen,
    stepOrder,
    toSave,
    type BlockName,
    type Draft,
    type LinkableArticle,
} from "./draft";
import type { OfferedBlocks } from "./offeredBlocks";
import { arrange, nodeSize, placed, type Positions } from "./layout";
import { PROCEDURE_HINTS } from "./hints";
import { MistakeList } from "./MistakeList";
import { targetOf, stepsWithMistakes } from "./mistakes";
import { ProcedurePanel } from "./ProcedurePanel";
import { isDirty, loaded, outcomeOf, reduce } from "./state";
import { StepForm } from "./StepForm";
import { StepNode, type StepFlowNode } from "./StepNode";
import { useLiveCheck } from "./useLiveCheck";

// Why: the canvas variables map React Flow's palette onto the design tokens, so the graph reads on the dark theme like the rest.
const CANVAS = [
    "[--xy-background-color:var(--background)]",
    "[--xy-background-pattern-dots-color:var(--border)]",
    "[--xy-edge-stroke:var(--muted-foreground)]",
    "[--xy-edge-stroke-selected:var(--primary)]",
    "[--xy-edge-stroke-width:1.5]",
    "[--xy-connectionline-stroke:var(--primary)]",
    "[--xy-handle-background-color:var(--muted-foreground)]",
    "[--xy-handle-border-color:var(--background)]",
    "[--xy-controls-button-background-color:var(--secondary)]",
    "[--xy-controls-button-background-color-hover:var(--accent)]",
    "[--xy-controls-button-color:var(--foreground)]",
    "[--xy-controls-button-color-hover:var(--foreground)]",
    "[--xy-controls-button-border-color:var(--border)]",
].join(" ");

// Why: React Flow compares node types by identity, so the map must outlive every render.
const NODE_TYPES = { step: StepNode };

function Notices({ state, onReload }: { state: ReturnType<typeof loaded>; onReload: () => void }) {
    const shown = noticeOf(state, onReload);
    return shown === null ? null : <div className="p-3">{shown}</div>;
}

function noticeOf(state: ReturnType<typeof loaded>, onReload: () => void) {
    const notice = state.notice;
    if (notice === null) return null;
    if (notice.kind === "stale") {
        return (
            <Alert variant="destructive" className="flex items-center gap-3">
                <span className="flex-1">
                    Not saved: {notice.message} Your drawing is still here; reloading replaces it with what was saved.
                </span>
                <Button type="button" size="sm" variant="outline" onClick={onReload}>
                    Reload
                </Button>
            </Alert>
        );
    }
    if (notice.kind === "refused") {
        return (
            <Alert variant="destructive">
                <p>Not saved: fix {notice.mistakes.length === 1 ? "this mistake" : "these mistakes"} first. Your drawing is unchanged.</p>
                <MistakeItems mistakes={notice.mistakes} />
            </Alert>
        );
    }
    return <Alert variant="destructive">Not saved: {notice.message}</Alert>;
}

function CreatorBody({
    bot,
    id,
    initial,
    facts,
    procedures,
    articles,
    actions,
    stored,
    offered,
}: {
    bot: string;
    id: string;
    initial: ProcedureDetail;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    actions: ActionView[];
    offered: OfferedBlocks;
    stored: Positions;
}) {
    const [state, dispatch] = useReducer(reduce, initial, loaded);
    const [positions, setPositions] = useState<Positions>(stored);
    const [params, setParams] = useSearchParams();
    // Why: the selected step is in the address, so a reload or a sent link opens the same step.
    const selected = params.get("step");
    const setSelected = (step: string | null) => {
        setParams(step === null ? {} : { step }, { replace: true });
    };
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);
    const [selectedEdge, setSelectedEdge] = useState<string | null>(null);
    const [reveal, setReveal] = useState<{ name: string; times: number } | null>(null);
    const [saving, setSaving] = useState(false);
    const [layoutFailure, setLayoutFailure] = useState<string | null>(null);
    const [changingId, setChangingId] = useState(false);
    const [, navigate] = useLocation();
    const flow = useReactFlow();
    const { draft } = state;
    const check = useLiveCheck(bot, id, state.revision, draft);
    const usage = useUsage(bot, "procedures", id, state.revision);
    const history = useResourceHistory(bot, "procedures", id, state.revision);
    const availability = useAvailability(bot);
    const dirty = isDirty(state);
    const changer = useMemo(() => renamer(bot, "procedures", id, state.revision), [bot, id, state.revision]);
    const drawn = useMemo(() => placed(draft, positions), [draft, positions]);
    // Why: a step is placed automatically once, and then stays, so drawing an edge never shuffles a graph the author is looking at.
    const fresh = Object.keys(drawn).filter((name) => !(name in positions));
    if (fresh.length > 0)
        setPositions({ ...positions, ...Object.fromEntries(fresh.flatMap((name) => (drawn[name] ? [[name, drawn[name]]] : []))) });
    const flagged = useMemo(() => (check.state === "done" ? stepsWithMistakes(check.mistakes, draft) : new Set<string>()), [check, draft]);

    useLeaveGuard(dirty, "You have unsaved changes. Leave and discard them?");

    useEffect(() => {
        if (reveal === null) return;
        const timer = setTimeout(() => {
            void flow.fitView({ nodes: [{ id: reveal.name }], duration: 200, maxZoom: 1, padding: 0.8 });
        }, 50);
        return () => {
            clearTimeout(timer);
        };
    }, [reveal, flow]);

    const leave = (href: string) => {
        if (!mayLeave()) return;
        navigate(href);
    };

    const edit = (change: (draft: Draft) => Draft) => {
        dispatch({ type: "edited", draft: change(draft) });
    };

    const keepPositions = (next: Positions) => {
        setPositions(next);
        setLayoutFailure(null);
        saveLayout(bot, id, { positions: next }).catch((error: unknown) => {
            setLayoutFailure(error instanceof Error ? error.message : String(error));
        });
    };

    const nodes: StepFlowNode[] = stepOrder(draft).map((name) => {
        const step = draft.steps[name] ?? {};
        const size = nodeSize(step);
        return {
            id: name,
            type: "step",
            position: drawn[name] ?? { x: 0, y: 0 },
            width: size.width,
            height: size.height,
            selected: name === selected,
            // Why: nodes are rebuilt on every edit, and a node without its size is measured again, its edges gone until that ends; the size is known, so an edge never waits on it.
            measured: size,
            data: { name, step, first: draft.firstStep === name, flagged: flagged.has(name) },
        };
    });
    const edges: FlowEdge[] = edgesOf(draft).map((edge) => ({
        id: edge.id,
        source: edge.source,
        sourceHandle: edge.handle,
        target: edge.target,
        targetHandle: "in",
        selected: edge.id === selectedEdge,
        markerEnd: { type: MarkerType.ArrowClosed, color: edge.id === selectedEdge ? "var(--primary)" : "var(--muted-foreground)" },
    }));

    const onNodesChange = (changes: NodeChange<StepFlowNode>[]) => {
        for (const change of changes) {
            if (change.type === "position" && change.position) setPositions({ ...drawn, [change.id]: change.position });
        }
    };

    const onConnect = (connection: Connection) => {
        edit((current) => connect(current, connection.source, connection.sourceHandle ?? "next", connection.target));
    };

    const add = (block: BlockName) => {
        const added = addStep(draft, block);
        dispatch({ type: "edited", draft: added.draft });
        setSelected(added.name);
        setReveal({ name: added.name, times: (reveal?.times ?? 0) + 1 });
    };

    const save = () => {
        setSaving(true);
        const sent = JSON.stringify(toSave(draft));
        void outcomeOf(saveProcedure(bot, id, toSave(draft), state.revision, false))
            .then((outcome) => {
                dispatch({ type: "saved", outcome, sent });
            })
            .finally(() => {
                setSaving(false);
            });
    };

    const removeProcedure = () => {
        setDeleteFailure(null);
        deleteProcedure(bot, id, state.revision)
            .then(() => {
                navigate(listHref(bot, "procedures"), { replace: true });
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

    return (
        <div className="flex h-full flex-col">
            <header className="flex items-center gap-3 border-b border-border px-4 py-2">
                <Button
                    type="button"
                    variant="link"
                    className="h-auto px-0"
                    onClick={() => {
                        leave(listHref(bot, "procedures"));
                    }}
                >
                    ← Back to procedures
                </Button>
                <h1 className="flex-1 text-xl font-bold">
                    Creator: {id} {dirty && <span className="text-sm font-normal text-warning">unsaved changes</span>}
                </h1>
                {layoutFailure !== null && <span className="text-sm text-destructive">The layout was not kept: {layoutFailure}</span>}
                <Button
                    type="button"
                    variant="outline"
                    onClick={() => {
                        keepPositions(arrange(draft));
                        setReveal({ name: stepOrder(draft)[0] ?? "", times: (reveal?.times ?? 0) + 1 });
                    }}
                >
                    Arrange
                </Button>
                <Button
                    type="button"
                    variant="outline"
                    disabled={dirty}
                    title={dirty ? "Save your changes first." : undefined}
                    onClick={() => {
                        setChangingId(true);
                    }}
                >
                    Change id
                </Button>
                <InfoTip about="changing the id" help={changeIdHelp("procedure", "id")} />
                <Button type="button" onClick={save} disabled={saving || !dirty}>
                    Save
                </Button>
            </header>
            {changingId && (
                <ChangeIdDialog
                    noun="procedure"
                    id={id}
                    preview={changer.preview}
                    apply={changer.apply}
                    onChanged={(newId) => {
                        navigate(editHref(bot, "procedures", newId), { replace: true });
                    }}
                    onCancel={() => {
                        setChangingId(false);
                    }}
                />
            )}
            <Notices state={state} onReload={reload} />
            <div className="flex min-h-0 flex-1">
                <div className="flex min-w-0 flex-1 flex-col">
                    <aside className="flex items-center gap-2 border-b border-border px-3 py-2" aria-label="Blocks">
                        <h2 className="flex items-center gap-1 text-base font-semibold">
                            Blocks
                            <InfoTip about="Blocks" help={PROCEDURE_HINTS.blocks} />
                        </h2>
                        {offered.blocks.map((block) => (
                            <Button
                                key={block}
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    add(block);
                                }}
                            >
                                {block}
                            </Button>
                        ))}
                        {offered.note !== null && <p className="text-sm text-muted-foreground">{offered.note}</p>}
                    </aside>
                    <div className="min-h-0 flex-1" aria-label="Graph">
                        <ReactFlow
                            colorMode="dark"
                            className={CANVAS}
                            nodes={nodes}
                            edges={edges}
                            nodeTypes={NODE_TYPES}
                            onNodesChange={onNodesChange}
                            onConnect={onConnect}
                            onNodeClick={(_, node) => {
                                setSelected(node.id);
                                setSelectedEdge(null);
                            }}
                            onEdgeClick={(_, edge) => {
                                setSelectedEdge(edge.id);
                            }}
                            onPaneClick={() => {
                                setSelected(null);
                                setSelectedEdge(null);
                            }}
                            onNodeDragStop={() => {
                                keepPositions(drawn);
                            }}
                            onNodesDelete={(removed) => {
                                let next = draft;
                                for (const node of removed) next = deleteStep(next, node.id);
                                dispatch({ type: "edited", draft: next });
                                setSelected(null);
                            }}
                            onEdgesDelete={(removed) => {
                                let next = draft;
                                for (const edge of removed) if (edge.sourceHandle) next = disconnect(next, edge.source, edge.sourceHandle);
                                dispatch({ type: "edited", draft: next });
                                setSelectedEdge(null);
                            }}
                            deleteKeyCode={["Backspace", "Delete"]}
                            nodesConnectable
                            fitView
                            minZoom={0.2}
                        >
                            <Background />
                            <Controls showInteractive={false} />
                        </ReactFlow>
                    </div>
                </div>
                <aside className="flex w-96 shrink-0 flex-col gap-6 overflow-y-auto border-l border-border p-4">
                    {selected !== null && selected in draft.steps ? (
                        <StepForm
                            key={selected}
                            bot={bot}
                            id={id}
                            name={selected}
                            draft={draft}
                            facts={factsSeen(draft, facts)}
                            procedures={procedures}
                            articles={articles}
                            actions={actions}
                            edit={edit}
                            onRenamed={setSelected}
                            onDeleted={() => {
                                setSelected(null);
                            }}
                            onLeave={leave}
                        />
                    ) : (
                        <ProcedurePanel
                            bot={bot}
                            id={id}
                            draft={draft}
                            articles={articles}
                            usage={usage}
                            history={history}
                            availability={availability}
                            deleteFailure={deleteFailure}
                            onDelete={removeProcedure}
                            edit={edit}
                        />
                    )}
                    <MistakeList
                        check={check}
                        onPick={(mistake) => {
                            const name = targetOf(mistake, draft);
                            setSelected(name);
                            if (name !== null) setReveal({ name, times: (reveal?.times ?? 0) + 1 });
                        }}
                    />
                    {check.state === "stale" && state.notice === null && (
                        <Alert variant="destructive" className="flex items-center gap-3">
                            <span className="flex-1">{check.message}</span>
                            <Button type="button" size="sm" variant="outline" onClick={reload}>
                                Reload
                            </Button>
                        </Alert>
                    )}
                </aside>
            </div>
        </div>
    );
}

/** The creator of one procedure: a graph of its steps, the form of the selected one, and the live list of mistakes. */
export function Creator(props: {
    bot: string;
    id: string;
    initial: ProcedureDetail;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    actions: ActionView[];
    stored: Positions;
    offered: OfferedBlocks;
}) {
    return (
        <ReactFlowProvider>
            <CreatorBody {...props} />
        </ReactFlowProvider>
    );
}
