import { Button, InfoTip, Input, Select } from "@maniumek/suwfrontkit";
import { useState } from "react";

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
import { ListRow } from "./ListRow";
import { ConditionsEditor, conditionsText } from "./RuleEditor";
import { StepForm } from "./StepForm";

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

const PLAIN = "bg-secondary text-foreground";
const BRANCH = "bg-warning/15 text-warning";

function NextRow({ last, props, list: ref }: { last: { next: string }; props: StepsProps; list: ListRef }) {
    const { draft, edit } = props;
    const names = Object.keys(draft.steps);
    const options = names.includes(last.next) ? names : [last.next, ...names];
    return (
        <ListRow
            kind="next"
            tag="Next:"
            tone={PLAIN}
            name={
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
            }
            remove={{
                label: "Remove this Next: row",
                steps: 0,
                onRemove: () => {
                    edit((current) => setListEnd(current, ref, null));
                },
            }}
        />
    );
}

function EndRow({ props, list: ref }: { props: StepsProps; list: ListRef }) {
    return (
        <ListRow
            kind="end"
            tag="End"
            tone={PLAIN}
            text={<span className="text-muted-foreground">The procedure ends here.</span>}
            remove={
                ref === null
                    ? undefined
                    : {
                          label: "Remove this End row",
                          steps: 0,
                          onRemove: () => {
                              props.edit((current) => setListEnd(current, ref, null));
                          },
                      }
            }
        />
    );
}

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
            <option value="" disabled hidden>
                + Add step
            </option>
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
    const ends = last === "end" || (ref === null && last === null);
    return (
        <div className="flex flex-col gap-1">
            {one.rows.map((row, index) => (
                <RowView key={row.name} row={row} number={index + 1} props={props} />
            ))}
            {last !== null && last !== "end" && <NextRow last={last} props={props} list={ref} />}
            <AddStep list={ref} props={props} label={label} />
            {ends && <EndRow props={props} list={ref} />}
        </div>
    );
}

function goesOnText(draft: Draft, ref: ListRef): string {
    const fall = fallOf(draft.list, ref);
    return fall === null ? "no steps yet" : `no steps: goes on to ${fall}`;
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
    const open = branch.when !== undefined && (!hasCondition || openConditions.includes(key));
    return (
        <div className="flex flex-col gap-1" data-branch={label} data-place={`branch:${row.name}:${index}`}>
            <ListRow
                kind="branch"
                tag={label}
                tone={BRANCH}
                text={
                    branch.when === undefined
                        ? empty && <span className="text-muted-foreground">{goesOnText(draft, ref)}</span>
                        : conditionsText(branch.when)
                }
                fold={
                    branch.when === undefined
                        ? undefined
                        : {
                              label: `Open ${label}${index === 0 ? "" : ` ${String(index)}`} of ${row.name}`,
                              open,
                              canFold: hasCondition,
                              onToggle: () => {
                                  onConditionOpen(key, !open);
                              },
                          }
                }
                note={
                    branch.when !== undefined && empty && <span className="shrink-0 text-muted-foreground">{goesOnText(draft, ref)}</span>
                }
                move={
                    branch.when === undefined
                        ? undefined
                        : index === 0
                          ? {
                                what: `If of ${row.name}`,
                                can: (direction) => canMoveIf(draft.list, row.name, direction),
                                onMove: (direction) => {
                                    edit((current) => moveIfBlock(current, row.name, direction));
                                },
                            }
                          : {
                                what: `Else if ${String(index)} of ${row.name}`,
                                can: (direction) => canSwapBranch(row, index, direction),
                                onMove: (direction) => {
                                    edit((current) => moveBranch(current, row.name, index, direction));
                                },
                            }
                }
                remove={
                    index === 0
                        ? {
                              label: "Remove this If",
                              steps: stepsInBranches(row),
                              onRemove: () => {
                                  edit((current) => removeIf(current, row.name));
                              },
                          }
                        : branch.when === undefined
                          ? undefined
                          : {
                                label: `Remove this ${label}`,
                                steps: stepsInBranch(branch),
                                onRemove: () => {
                                    edit((current) => removeBranch(current, row.name, index));
                                },
                            }
                }
            />
            {branch.when !== undefined && open && (
                <div className="ml-6 rounded-md border border-border p-3">
                    <ConditionsEditor
                        when={branch.when}
                        facts={facts}
                        onChange={(when) => {
                            edit((current) => setConditions(current, row.name, index, when));
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
                    <ListRow
                        kind="failure"
                        tag="On failure"
                        tone={BRANCH}
                        text={
                            row.failure === undefined ? (
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
                            )
                        }
                    />
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
            {isAction && (
                <div className="flex flex-col gap-1" data-place={`unavailable:${row.name}`}>
                    <ListRow
                        kind="unavailable"
                        tag="When unavailable"
                        tone={BRANCH}
                        text={
                            (row.unavailable === undefined || (row.unavailable.rows.length === 0 && row.unavailable.last === null)) && (
                                <span className="text-muted-foreground">no steps: as on failure</span>
                            )
                        }
                        note={<InfoTip about="When unavailable" help={PROCEDURE_HINTS.onUnavailable} />}
                    />
                    {beside(`unavailable:${row.name}`)}
                    <div className="ml-6 border-l border-border pl-3">
                        <StepsOf
                            list={{ step: row.name, arm: "unavailable" }}
                            props={props}
                            label={`Add step to When unavailable of ${row.name}`}
                        />
                    </div>
                </div>
            )}
            {row.success && (
                <div className="flex flex-col gap-1" data-place={`success:${row.name}`}>
                    <ListRow
                        kind="success"
                        tag="On success"
                        tone={BRANCH}
                        text={
                            row.success.rows.length === 0 &&
                            row.success.last === null && (
                                <span className="text-muted-foreground">{goesOnText(draft, { step: row.name, arm: "success" })}</span>
                            )
                        }
                    />
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
            <ListRow
                kind="step"
                data={{ "data-step": row.name, "data-place": `step:${row.name}` }}
                number={number}
                tag={block === null ? "No block" : BLOCK_LABELS[block]}
                tone={block === null ? "bg-secondary text-muted-foreground" : TAG_TONES[block]}
                name={
                    <StepName
                        key={row.name}
                        name={row.name}
                        draft={draft}
                        edit={edit}
                        onRenamed={(to) => {
                            if (open) onSelect(to);
                        }}
                    />
                }
                text={rowText(step)}
                fold={{
                    label: `Open step ${row.name}`,
                    open,
                    canFold: true,
                    title: block === null ? undefined : `Format: ${block}`,
                    onToggle: () => {
                        onSelect(open ? null : row.name);
                    },
                }}
                move={{
                    what: row.name,
                    can: () => true,
                    onMove: (direction) => {
                        edit((current) => moveStep(current, row.name, direction));
                    },
                }}
                remove={{
                    label: `Delete step ${row.name}`,
                    steps: 0,
                    onRemove: () => {
                        const why = whyNotDeletable(findRow(draft.list, row.name));
                        setRefusal(why);
                        if (why !== null) return;
                        edit((current) => deleteStep(current, row.name));
                        if (open) onSelect(null);
                    },
                }}
            />
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
                                <ListRow
                                    kind="not-connected"
                                    tag={block === null ? "No block" : BLOCK_LABELS[block]}
                                    tone={block === null ? "bg-secondary text-muted-foreground" : TAG_TONES[block]}
                                    name={<span className="shrink-0 font-mono text-xs">{name}</span>}
                                    text={rowText(step)}
                                    note={
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
                                    }
                                    remove={{
                                        label: `Delete step ${name}`,
                                        steps: 0,
                                        onRemove: () => {
                                            edit((current) => deleteStep(current, name));
                                        },
                                    }}
                                />
                                {beside(`step:${name}`)}
                            </div>
                        );
                    })}
                </section>
            )}
        </div>
    );
}
