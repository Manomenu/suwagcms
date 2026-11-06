import { useState, type ReactNode } from "react";

import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import type { ActionView, Fact } from "./api";
import {
    addElse,
    addElseIf,
    addIf,
    addStep,
    canEnd,
    canTakeIf,
    connectStep,
    deleteStep,
    moveBranch,
    moveIfBlock,
    moveStep,
    openFailure,
    removeBranch,
    removeIf,
    renameStep,
    setConditions,
    setListEnd,
    stepsInBranch,
    stepsInBranches,
    whyNotDeletable,
    type BlockName,
    type Draft,
    type LinkableArticle,
    type LinkableBase,
    type Step,
} from "./draft";
import { PROCEDURE_HINTS } from "./hints";
import type { Beside } from "./KnowledgeForm";
import { canMoveIf, canSwapBranch, fallOf, findRow, listAt, type ListEnd, type ListRef, type StepList, type StepRow } from "./list";
import { ConditionsEditor, conditionsText } from "./RuleEditor";
import { StepForm } from "./StepForm";

/** The writer's word for each block: Ask because a collect lists facts, and Tell because an inform has the model word the points. */
export const BLOCK_LABELS: Record<BlockName, string> = {
    listen: "Listen",
    collect: "Ask",
    say: "Say",
    assign: "Set",
    inform: "Tell",
    start: "Start",
    action: "Action",
};

const TAG_TONES: Record<BlockName, string> = {
    listen: "bg-secondary text-foreground",
    collect: "bg-primary/15 text-primary",
    say: "bg-accent text-accent-foreground",
    assign: "bg-secondary text-foreground",
    inform: "bg-success/15 text-success",
    start: "bg-primary/15 text-primary",
    action: "bg-accent text-accent-foreground",
};

export function blockOf(step: Step): BlockName | null {
    if (step.listen) return "listen";
    if (step.collect) return "collect";
    if (step.say !== undefined) return "say";
    if (step.assign) return "assign";
    if (step.inform) return "inform";
    if (step.start !== undefined) return "start";
    if (step.action !== undefined) return "action";
    return null;
}

/** What a row says of its step when it is not open. */
export function rowText(step: Step): string {
    if (step.listen) return "Wait for the next message";
    if (step.collect) return step.collect.length === 0 ? "No facts yet" : step.collect.join(", ");
    if (step.say !== undefined) return step.say === "" ? "No text yet" : step.say;
    if (step.assign) {
        const pairs = Object.entries(step.assign);
        return pairs.length === 0 ? "No facts yet" : pairs.map(([fact, value]) => `${fact} = ${value}`).join(", ");
    }
    if (step.inform) {
        const count = Object.keys(step.inform.points ?? {}).length;
        return `${step.inform.instruction === "" ? "No instruction yet" : step.inform.instruction} (${count} ${count === 1 ? "point" : "points"})`;
    }
    if (step.start !== undefined) return step.start === "" ? "No procedure yet" : step.start;
    if (step.action !== undefined) return step.action === "" ? "No action yet" : step.action;
    return "No block yet";
}

function Tag({ tone, children }: { tone: string; children: ReactNode }) {
    return <span className={`inline-flex shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold ${tone}`}>{children}</span>;
}

export interface StepsProps {
    bot: string;
    id: string;
    draft: Draft;
    facts: Fact[];
    procedures: string[];
    offered: readonly BlockName[];
    showKnowledge: boolean;
    bases: LinkableBase[];
    articles: LinkableArticle[];
    actions: ActionView[];
    selected: string | null;
    beside: Beside;
    edit: (change: (draft: Draft) => Draft) => void;
    onSelect: (step: string | null) => void;
    /** The If and Else if rows unfolded, as `step:branch`. Unlike a step, several can be open at once, each on its own. */
    openConditions: readonly string[];
    onConditionOpen: (row: string, open: boolean) => void;
    onLeave: (href: string) => void;
}

function StepName({
    name,
    draft,
    edit,
    onRenamed,
}: {
    name: string;
    draft: Draft;
    edit: StepsProps["edit"];
    onRenamed: (to: string) => void;
}) {
    const [typed, setTyped] = useState(name);
    const taken = typed !== name && typed in draft.steps;
    return (
        <span className="flex shrink-0 flex-col">
            <Input
                aria-label={`Name of step ${name}`}
                title={PROCEDURE_HINTS.stepName}
                aria-invalid={taken}
                className="h-7 w-40 px-1.5 font-mono text-xs"
                value={typed}
                onClick={(event) => {
                    event.stopPropagation();
                }}
                onChange={(event) => {
                    setTyped(event.target.value);
                }}
                onBlur={() => {
                    if (typed !== name && !taken && typed !== "") {
                        edit((current) => renameStep(current, name, typed));
                        onRenamed(typed);
                    } else {
                        setTyped(name);
                    }
                }}
            />
            {taken && <span className="text-xs text-destructive">Another step already has this name.</span>}
        </span>
    );
}

/** Asks once before a control takes steps with it, in the place of the control itself, since the page has no other dialog. */
function RemoveButton({ label, steps, onRemove }: { label: string; steps: number; onRemove: () => void }) {
    const [asking, setAsking] = useState(false);
    if (asking) {
        return (
            <span className="flex items-center gap-1 text-sm">
                {steps} {steps === 1 ? "step goes" : "steps go"} with it.
                <Button type="button" variant="destructive" size="xs" onClick={onRemove}>
                    Remove them
                </Button>
                <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    onClick={() => {
                        setAsking(false);
                    }}
                >
                    Keep
                </Button>
            </span>
        );
    }
    return (
        <Button
            type="button"
            variant="ghost"
            size="xs"
            onClick={() => {
                if (steps === 0) onRemove();
                else setAsking(true);
            }}
        >
            {label}
        </Button>
    );
}

function NextRow({ last, props, list: ref }: { last: { next: string }; props: StepsProps; list: ListRef }) {
    const { draft, edit } = props;
    const names = Object.keys(draft.steps);
    const options = names.includes(last.next) ? names : [last.next, ...names];
    return (
        <div className="flex items-center gap-2 py-1 text-sm" data-row="next">
            <Tag tone="bg-secondary text-foreground">Next:</Tag>
            <Select
                aria-label="Next step"
                title={PROCEDURE_HINTS.next}
                value={last.next}
                onChange={(event) => {
                    edit((current) => setListEnd(current, ref, { next: event.target.value }));
                }}
            >
                {options.map((name) => (
                    <option key={name} value={name}>
                        {name}
                    </option>
                ))}
            </Select>
            <Button
                type="button"
                variant="ghost"
                size="xs"
                aria-label="Remove this Next: row"
                onClick={() => {
                    edit((current) => setListEnd(current, ref, null));
                }}
            >
                ✕
            </Button>
        </div>
    );
}

function EndRow({ props, list: ref }: { props: StepsProps; list: ListRef }) {
    return (
        <div className="flex items-center gap-2 py-1 text-sm" data-row="end">
            <Tag tone="bg-secondary text-foreground">End</Tag>
            <span className="text-muted-foreground">The procedure ends here.</span>
            <Button
                type="button"
                variant="ghost"
                size="xs"
                aria-label="Remove this End row"
                onClick={() => {
                    props.edit((current) => setListEnd(current, ref, null));
                }}
            >
                ✕
            </Button>
        </div>
    );
}

/** The one control at the end of a list: it adds what the bot declares, and an If, a Next: or an End where each can stand. */
function AddStep({ list: ref, props, label }: { list: ListRef; props: StepsProps; label: string }) {
    const { draft, offered, edit, onSelect } = props;
    const one = listAt(draft.list, ref);
    const lastRow = one.rows[one.rows.length - 1];
    const open = one.last === null;
    return (
        <Select
            aria-label={label}
            className="w-44"
            value=""
            onChange={(event) => {
                const choice = event.target.value;
                if (choice === "next") {
                    const first = Object.keys(draft.steps)[0] ?? "";
                    edit((current) => setListEnd(current, ref, { next: first }));
                } else if (choice === "end") {
                    edit((current) => setListEnd(current, ref, "end"));
                } else if (choice === "if" && lastRow) {
                    edit((current) => addIf(current, lastRow.name));
                } else if (choice !== "") {
                    const added = addStep(draft, ref, choice as BlockName);
                    edit(() => added.draft);
                    onSelect(added.name);
                }
            }}
        >
            <option value="">+ Add step</option>
            {offered.map((block) => (
                <option key={block} value={block}>
                    {BLOCK_LABELS[block]}
                </option>
            ))}
            {canTakeIf(lastRow) && <option value="if">If</option>}
            {open && Object.keys(draft.steps).length > 0 && <option value="next">Next:</option>}
            {open && canEnd(draft, ref) && <option value="end">End</option>}
        </Select>
    );
}

function StepsOf({ list: ref, props, label }: { list: ListRef; props: StepsProps; label: string }) {
    const one: StepList = listAt(props.draft.list, ref);
    const last: ListEnd = one.last;
    return (
        <div className="flex flex-col gap-1">
            {one.rows.map((row, index) => (
                <RowView key={row.name} row={row} number={index + 1} props={props} />
            ))}
            {last !== null && last !== "end" && <NextRow last={last} props={props} list={ref} />}
            {last === "end" && <EndRow props={props} list={ref} />}
            <AddStep list={ref} props={props} label={label} />
        </div>
    );
}

function goesOnText(draft: Draft, ref: ListRef): string {
    const fall = fallOf(draft.list, ref);
    return fall === null ? "no steps yet" : `no steps: goes on to ${fall}`;
}

function Arrows({
    what,
    can,
    onMove,
}: {
    what: string;
    can: (direction: "up" | "down") => boolean;
    onMove: (direction: "up" | "down") => void;
}) {
    return (
        <span className="flex shrink-0 items-center">
            {(["up", "down"] as const).map((direction) => (
                <Button
                    key={direction}
                    type="button"
                    variant="ghost"
                    size="xs"
                    aria-label={`Move ${what} ${direction}`}
                    disabled={!can(direction)}
                    onClick={() => {
                        onMove(direction);
                    }}
                >
                    {direction === "up" ? "↑" : "↓"}
                </Button>
            ))}
        </span>
    );
}

function BranchView({ row, index, props }: { row: StepRow; index: number; props: StepsProps }) {
    const { draft, facts, edit, beside, openConditions, onConditionOpen } = props;
    const branch = row.branches?.[index];
    if (branch === undefined) return null;
    const ref = { step: row.name, arm: index };
    const empty = branch.list.rows.length === 0 && branch.list.last === null;
    const label = branch.when === undefined ? "Else" : index === 0 ? "If" : "Else if";
    const key = `${row.name}:${String(index)}`;
    const hasCondition = Object.keys(branch.when ?? {}).length > 0;
    // Why: a row with no condition cannot fold, or an If would again show nothing to set one with.
    const open = branch.when !== undefined && (!hasCondition || openConditions.includes(key));
    return (
        <div className="flex flex-col gap-1" data-branch={label} data-place={`branch:${row.name}:${index}`}>
            <div className={`flex flex-wrap items-center gap-2 rounded-md px-1 py-1 text-sm ${open ? "bg-secondary" : ""}`}>
                <Tag tone="bg-warning/15 text-warning">{label}</Tag>
                {branch.when !== undefined && (
                    <button
                        type="button"
                        className="min-w-0 flex-1 cursor-pointer truncate text-left disabled:cursor-default"
                        aria-expanded={open}
                        aria-label={`Open ${label}${index === 0 ? "" : ` ${String(index)}`} of ${row.name}`}
                        disabled={!hasCondition}
                        onClick={() => {
                            onConditionOpen(key, !open);
                        }}
                    >
                        {conditionsText(branch.when)}
                    </button>
                )}
                {empty && <span className="text-muted-foreground">{goesOnText(draft, { ...ref })}</span>}
                {branch.when !== undefined &&
                    (index === 0 ? (
                        <Arrows
                            what={`If of ${row.name}`}
                            can={(direction) => canMoveIf(draft.list, row.name, direction)}
                            onMove={(direction) => {
                                edit((current) => moveIfBlock(current, row.name, direction));
                            }}
                        />
                    ) : (
                        <Arrows
                            what={`Else if ${String(index)} of ${row.name}`}
                            can={(direction) => canSwapBranch(row, index, direction)}
                            onMove={(direction) => {
                                edit((current) => moveBranch(current, row.name, index, direction));
                            }}
                        />
                    ))}
                {index === 0 ? (
                    <RemoveButton
                        label="Remove this If"
                        steps={stepsInBranches(row)}
                        onRemove={() => {
                            edit((current) => removeIf(current, row.name));
                        }}
                    />
                ) : (
                    branch.when !== undefined && (
                        <RemoveButton
                            label={`Remove this ${label}`}
                            steps={stepsInBranch(branch)}
                            onRemove={() => {
                                edit((current) => removeBranch(current, row.name, index));
                            }}
                        />
                    )
                )}
            </div>
            {branch.when !== undefined && open && (
                <div className="ml-6 rounded-md border border-border p-3">
                    <ConditionsEditor
                        when={branch.when}
                        facts={facts}
                        onChange={(when) => {
                            edit((current) => setConditions(current, row.name, index, when));
                            // Why: choosing the first fact gives the row a condition, and it must not fold under the hand that is setting it.
                            if (!openConditions.includes(key)) onConditionOpen(key, true);
                        }}
                    />
                </div>
            )}
            {beside(`branch:${row.name}:${index}`)}
            <div className="ml-6 border-l border-border pl-3">
                <StepsOf list={ref} props={props} label={`Add step to ${label} of ${row.name}`} />
            </div>
        </div>
    );
}

function ArmsView({ row, props }: { row: StepRow; props: StepsProps }) {
    const { draft, edit, beside } = props;
    const step = draft.steps[row.name];
    const isAction = step?.action !== undefined;
    return (
        <>
            {isAction && (
                <div className="flex flex-col gap-1" data-place={`failure:${row.name}`}>
                    <div className="flex flex-wrap items-center gap-2 text-sm">
                        <Tag tone="bg-warning/15 text-warning">On failure</Tag>
                        {row.failure === undefined ? (
                            <Button
                                type="button"
                                variant="outline"
                                size="xs"
                                onClick={() => {
                                    edit((current) => openFailure(current, row.name));
                                }}
                            >
                                Set what happens
                            </Button>
                        ) : (
                            row.failure.rows.length === 0 &&
                            row.failure.last === null && (
                                <span className="text-muted-foreground">{goesOnText(draft, { step: row.name, arm: "failure" })}</span>
                            )
                        )}
                    </div>
                    {beside(`failure:${row.name}`)}
                    {row.failure && (
                        <div className="ml-6 border-l border-border pl-3">
                            <StepsOf
                                list={{ step: row.name, arm: "failure" }}
                                props={props}
                                label={`Add step to On failure of ${row.name}`}
                            />
                        </div>
                    )}
                </div>
            )}
            {row.success && (
                <div className="flex flex-col gap-1" data-place={`success:${row.name}`}>
                    <div className="flex flex-wrap items-center gap-2 text-sm">
                        <Tag tone="bg-warning/15 text-warning">On success</Tag>
                        {row.success.rows.length === 0 && row.success.last === null && (
                            <span className="text-muted-foreground">{goesOnText(draft, { step: row.name, arm: "success" })}</span>
                        )}
                    </div>
                    <div className="ml-6 border-l border-border pl-3">
                        <StepsOf list={{ step: row.name, arm: "success" }} props={props} label={`Add step to On success of ${row.name}`} />
                    </div>
                </div>
            )}
            {row.branches && (
                <div className="flex flex-col gap-1">
                    {row.branches.map((_, index) => (
                        <BranchView key={index} row={row} index={index} props={props} />
                    ))}
                    <div className="flex flex-wrap items-center gap-2">
                        <Button
                            type="button"
                            variant="outline"
                            size="xs"
                            onClick={() => {
                                edit((current) => addElseIf(current, row.name));
                            }}
                        >
                            + Else if
                        </Button>
                        {row.branches[row.branches.length - 1]?.when !== undefined && (
                            <Button
                                type="button"
                                variant="outline"
                                size="xs"
                                onClick={() => {
                                    edit((current) => addElse(current, row.name));
                                }}
                            >
                                + Else
                            </Button>
                        )}
                    </div>
                </div>
            )}
        </>
    );
}

function RowView({ row, number, props }: { row: StepRow; number: number; props: StepsProps }) {
    const { draft, facts, procedures, id, bot, edit, selected, onSelect, onLeave, showKnowledge, bases, articles, actions, beside } = props;
    const [refusal, setRefusal] = useState<string | null>(null);
    const step = draft.steps[row.name] ?? {};
    const block = blockOf(step);
    const open = selected === row.name;
    return (
        <div className="flex flex-col gap-1">
            <div
                className={`flex flex-wrap items-center gap-2 rounded-md px-1 py-1 text-sm ${open ? "bg-secondary" : ""}`}
                data-row="step"
                data-step={row.name}
                data-place={`step:${row.name}`}
            >
                <span className="w-5 shrink-0 text-right text-xs text-muted-foreground">{number}.</span>
                <StepName
                    key={row.name}
                    name={row.name}
                    draft={draft}
                    edit={edit}
                    onRenamed={(to) => {
                        if (open) onSelect(to);
                    }}
                />
                {block === null ? (
                    <Tag tone="bg-secondary text-muted-foreground">No block</Tag>
                ) : (
                    <Tag tone={TAG_TONES[block]}>{BLOCK_LABELS[block]}</Tag>
                )}
                <button
                    type="button"
                    className="min-w-0 flex-1 cursor-pointer truncate text-left"
                    aria-expanded={open}
                    aria-label={`Open step ${row.name}`}
                    title={block === null ? undefined : `Format: ${block}`}
                    onClick={() => {
                        onSelect(open ? null : row.name);
                    }}
                >
                    {rowText(step)}
                </button>
                <span className="flex shrink-0 items-center">
                    <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        aria-label={`Move ${row.name} up`}
                        onClick={() => {
                            edit((current) => moveStep(current, row.name, "up"));
                        }}
                    >
                        ↑
                    </Button>
                    <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        aria-label={`Move ${row.name} down`}
                        onClick={() => {
                            edit((current) => moveStep(current, row.name, "down"));
                        }}
                    >
                        ↓
                    </Button>
                    <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        aria-label={`Delete step ${row.name}`}
                        onClick={() => {
                            const why = whyNotDeletable(findRow(draft.list, row.name));
                            setRefusal(why);
                            if (why !== null) return;
                            edit((current) => deleteStep(current, row.name));
                            if (open) onSelect(null);
                        }}
                    >
                        ✕
                    </Button>
                </span>
            </div>
            {refusal !== null && <p className="ml-6 text-sm text-destructive">{refusal}</p>}
            {beside(`step:${row.name}`)}
            {open && (
                <div className="ml-6 rounded-md border border-border p-3">
                    <StepForm
                        bot={bot}
                        id={id}
                        name={row.name}
                        step={step}
                        facts={facts}
                        procedures={procedures}
                        showKnowledge={showKnowledge}
                        bases={bases}
                        articles={articles}
                        actions={actions}
                        beside={beside}
                        edit={edit}
                        onLeave={onLeave}
                    />
                </div>
            )}
            <ArmsView row={row} props={props} />
        </div>
    );
}

/** The steps of a procedure as a page reads them: a numbered list, an If with its indented branches, and what nothing leads to. */
export function StepsEditor(props: StepsProps) {
    const { draft, edit, beside } = props;
    return (
        <div className="flex flex-col gap-3">
            <StepsOf list={null} props={props} label="Add step" />
            {draft.notConnected.length > 0 && (
                <section aria-label="Not connected" className="flex flex-col gap-1">
                    <h3 className="text-sm font-semibold">Not connected</h3>
                    {draft.notConnected.map((name) => {
                        const step = draft.steps[name] ?? {};
                        const block = blockOf(step);
                        return (
                            <div key={name} className="flex flex-col gap-1" data-place={`step:${name}`}>
                                <div className="flex flex-wrap items-center gap-2 text-sm" data-row="not-connected">
                                    <span className="font-mono text-xs">{name}</span>
                                    {block !== null && <Tag tone={TAG_TONES[block]}>{BLOCK_LABELS[block]}</Tag>}
                                    <span className="min-w-0 flex-1 truncate">{rowText(step)}</span>
                                    <Button
                                        type="button"
                                        variant="outline"
                                        size="xs"
                                        onClick={() => {
                                            edit((current) => connectStep(current, name));
                                        }}
                                    >
                                        Move into the list
                                    </Button>
                                    <Button
                                        type="button"
                                        variant="ghost"
                                        size="xs"
                                        aria-label={`Delete step ${name}`}
                                        onClick={() => {
                                            edit((current) => deleteStep(current, name));
                                        }}
                                    >
                                        ✕
                                    </Button>
                                </div>
                                {beside(`step:${name}`)}
                            </div>
                        );
                    })}
                </section>
            )}
        </div>
    );
}
