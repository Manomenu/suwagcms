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
import { Link, useLocation } from "wouter";

import { Alert } from "@/design/alert";
import { Button } from "@/design/button";

import { fetchProcedure, saveLayout, saveProcedure, type Fact, type ProcedureDetail } from "./api";
import { addStep, BLOCKS, connect, deleteStep, disconnect, edgesOf, stepOrder, toSave, type BlockName, type Draft } from "./draft";
import { arrange, nodeSize, placed, type Positions } from "./layout";
import { MistakeList } from "./MistakeList";
import { targetOf, stepsWithMistakes } from "./mistakes";
import { ProcedurePanel } from "./ProcedurePanel";
import { isDirty, loaded, outcomeOf, reduce } from "./state";
import { StepForm } from "./StepForm";
import { StepNode, type StepFlowNode } from "./StepNode";
import { useLiveCheck } from "./useLiveCheck";

// Why: React Flow compares node types by identity, so the map must outlive every render.
const NODE_TYPES = { step: StepNode };

function Notices({ state, onReload }: { state: ReturnType<typeof loaded>; onReload: () => void }) {
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
                <ul className="list-disc pl-4">
                    {notice.mistakes.map((mistake) => (
                        <li key={`${mistake.code}:${mistake.path}`}>
                            {mistake.message} {mistake.fix}
                        </li>
                    ))}
                </ul>
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
    stored,
}: {
    bot: string;
    id: string;
    initial: ProcedureDetail;
    facts: Fact[];
    stored: Positions;
}) {
    const [state, dispatch] = useReducer(reduce, initial, loaded);
    const [positions, setPositions] = useState<Positions>(stored);
    const [selected, setSelected] = useState<string | null>(null);
    const [selectedEdge, setSelectedEdge] = useState<string | null>(null);
    const [reveal, setReveal] = useState<{ name: string; times: number } | null>(null);
    const [saving, setSaving] = useState(false);
    const [layoutFailure, setLayoutFailure] = useState<string | null>(null);
    const [, navigate] = useLocation();
    const flow = useReactFlow();
    const { draft } = state;
    const check = useLiveCheck(bot, id, state.revision, draft);
    const dirty = isDirty(state);
    const drawn = useMemo(() => placed(draft, positions), [draft, positions]);
    // Why: a step is placed automatically once, and then stays, so drawing an edge never shuffles a graph the author is looking at.
    const fresh = Object.keys(drawn).filter((name) => !(name in positions));
    if (fresh.length > 0)
        setPositions({ ...positions, ...Object.fromEntries(fresh.flatMap((name) => (drawn[name] ? [[name, drawn[name]]] : []))) });
    const flagged = useMemo(() => (check.state === "done" ? stepsWithMistakes(check.mistakes, draft) : new Set<string>()), [check, draft]);

    useEffect(() => {
        if (!dirty) return;
        const warn = (event: BeforeUnloadEvent) => {
            event.preventDefault();
        };
        window.addEventListener("beforeunload", warn);
        return () => {
            window.removeEventListener("beforeunload", warn);
        };
    }, [dirty]);

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
        if (dirty && !window.confirm("You have unsaved changes. Leave and discard them?")) return;
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
        markerEnd: { type: MarkerType.ArrowClosed },
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

    const reload = () => {
        void fetchProcedure(bot, id, new AbortController().signal).then((detail) => {
            dispatch({ type: "loaded", detail });
            setSelected(null);
        });
    };

    return (
        <div className="flex h-screen flex-col">
            <header className="flex items-center gap-3 border-b px-4 py-2">
                <Link
                    href={`~/bots/${encodeURIComponent(bot)}/procedures`}
                    className="text-sm text-primary underline underline-offset-4"
                    onClick={(event) => {
                        event.preventDefault();
                        leave(`/bots/${encodeURIComponent(bot)}/procedures`);
                    }}
                >
                    Procedures
                </Link>
                <h1 className="flex-1 text-xl font-semibold">
                    Creator: {id} {dirty && <span className="text-sm font-normal text-amber-700">unsaved changes</span>}
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
                <Button type="button" onClick={save} disabled={saving || !dirty}>
                    Save
                </Button>
            </header>
            <Notices state={state} onReload={reload} />
            <div className="flex min-h-0 flex-1">
                <aside className="flex w-36 flex-col gap-2 border-r p-3" aria-label="Blocks">
                    <h2 className="text-sm font-semibold">Blocks</h2>
                    {BLOCKS.map((block) => (
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
                </aside>
                <div className="min-w-0 flex-1" aria-label="Graph">
                    <ReactFlow
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
                <aside className="flex w-96 flex-col gap-6 overflow-y-auto border-l p-4">
                    {selected !== null && selected in draft.steps ? (
                        <StepForm
                            key={selected}
                            bot={bot}
                            name={selected}
                            draft={draft}
                            facts={facts}
                            edit={edit}
                            onRenamed={setSelected}
                            onDeleted={() => {
                                setSelected(null);
                            }}
                            onLeave={leave}
                        />
                    ) : (
                        <ProcedurePanel id={id} draft={draft} edit={edit} />
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
export function Creator(props: { bot: string; id: string; initial: ProcedureDetail; facts: Fact[]; stored: Positions }) {
    return (
        <ReactFlowProvider>
            <CreatorBody {...props} />
        </ReactFlowProvider>
    );
}
