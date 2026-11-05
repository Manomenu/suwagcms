import { useState } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { ActionView, Fact } from "./api";
import { Checklist } from "./Checklist";
import { ContextForm } from "./ContextForm";
import {
    addPoint,
    addRule,
    collectable,
    connect,
    disconnect,
    deleteStep,
    FAILURE,
    factsThatFit,
    markFirst,
    removePoint,
    renamePoint,
    renameStep,
    setBlock,
    startable,
    valuesOffered,
    withStored,
    type Draft,
    type Inform,
    type LinkableArticle,
    type Step,
} from "./draft";
import { RuleEditor, TargetSelect } from "./RuleEditor";

function NoFacts({ bot, onLeave }: { bot: string; onLeave: (href: string) => void }) {
    const href = `/bots/${encodeURIComponent(bot)}/facts`;
    return (
        <p className="text-sm text-muted-foreground">
            This bot has no facts yet.{" "}
            <Link
                href={`~${href}`}
                className="text-anchor underline underline-offset-4"
                onClick={(event) => {
                    event.preventDefault();
                    onLeave(href);
                }}
            >
                Add them on the Facts screen
            </Link>
            .
        </p>
    );
}

function CollectForm({
    picked,
    facts,
    bot,
    onLeave,
    onChange,
}: {
    picked: string[];
    facts: Fact[];
    bot: string;
    onLeave: (href: string) => void;
    onChange: (facts: string[]) => void;
}) {
    const offered = collectable(facts);
    const names = withStored(
        offered.map((fact) => fact.name),
        picked,
    );
    if (names.length === 0) return <NoFacts bot={bot} onLeave={onLeave} />;
    return <Checklist legend="Collect these facts" names={names} picked={picked} onChange={onChange} />;
}

function AssignForm({
    assigned,
    facts,
    bot,
    onLeave,
    onChange,
}: {
    assigned: Record<string, string>;
    facts: Fact[];
    bot: string;
    onLeave: (href: string) => void;
    onChange: (assign: Record<string, string>) => void;
}) {
    const entries = Object.entries(assigned);
    const free = facts.filter((fact) => !(fact.name in assigned));
    if (facts.length === 0 && entries.length === 0) return <NoFacts bot={bot} onLeave={onLeave} />;
    return (
        <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium">Assign these values</legend>
            {entries.map(([name, value]) => {
                const values = valuesOffered(facts.find((fact) => fact.name === name));
                const set = (next: string) => {
                    onChange({ ...assigned, [name]: next });
                };
                return (
                    <div key={name} className="flex items-center gap-1 text-sm">
                        <span className="w-28 shrink-0 truncate" title={name}>
                            {name}
                        </span>
                        {values === null ? (
                            <Input
                                aria-label={`Value of ${name}`}
                                value={value}
                                onChange={(event) => {
                                    set(event.target.value);
                                }}
                            />
                        ) : (
                            <Select
                                aria-label={`Value of ${name}`}
                                className="flex-1"
                                value={value}
                                onChange={(event) => {
                                    set(event.target.value);
                                }}
                            >
                                <option value="">(choose)</option>
                                {values.map((option) => (
                                    <option key={option} value={option}>
                                        {option}
                                    </option>
                                ))}
                            </Select>
                        )}
                        <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={`Stop assigning ${name}`}
                            onClick={() => {
                                onChange(Object.fromEntries(entries.filter(([other]) => other !== name)));
                            }}
                        >
                            ✕
                        </Button>
                    </div>
                );
            })}
            {free[0] !== undefined && (
                <Select
                    aria-label="Assign another fact"
                    value=""
                    onChange={(event) => {
                        if (event.target.value !== "") onChange({ ...assigned, [event.target.value]: "" });
                    }}
                >
                    <option value="">Add a fact to assign…</option>
                    {free.map((fact) => (
                        <option key={fact.name} value={fact.name}>
                            {fact.name}
                        </option>
                    ))}
                </Select>
            )}
        </fieldset>
    );
}

function StartForm({ target, offered, onChange }: { target: string; offered: string[]; onChange: (start: string) => void }) {
    // Why: a target the step already names stays visible even when it is not offered, so opening a procedure never hides what is stored.
    const options = offered.includes(target) || target === "" ? offered : [target, ...offered];
    return (
        <label className="flex flex-col gap-1 text-sm font-medium">
            Start this procedure
            <Select
                className="w-full"
                value={target}
                onChange={(event) => {
                    onChange(event.target.value);
                }}
            >
                <option value="">(choose)</option>
                {options.map((id) => (
                    <option key={id} value={id}>
                        {id}
                    </option>
                ))}
            </Select>
        </label>
    );
}

function PointsForm({ points, onChange }: { points: Record<string, string>; onChange: (points: Record<string, string>) => void }) {
    const entries = Object.entries(points);
    return (
        <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium">Points the customer must hear</legend>
            {entries.map(([name, text], index) => (
                // Why: a point's name is what is being typed, so its place in the list is what keeps the field focused.
                <div key={index} className="flex items-start gap-1">
                    <Input
                        aria-label={`Name of point ${index + 1}`}
                        className="w-32 shrink-0"
                        value={name}
                        onChange={(event) => {
                            onChange(renamePoint(points, name, event.target.value));
                        }}
                    />
                    <Textarea
                        aria-label={`Text of point ${index + 1}`}
                        value={text}
                        onChange={(event) => {
                            onChange({ ...points, [name]: event.target.value });
                        }}
                    />
                    <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={`Remove point ${index + 1}`}
                        onClick={() => {
                            onChange(removePoint(points, name));
                        }}
                    >
                        ✕
                    </Button>
                </div>
            ))}
            <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => {
                    onChange(addPoint(points));
                }}
            >
                Add point
            </Button>
        </fieldset>
    );
}

function InformForm({ inform, articles, onChange }: { inform: Inform; articles: LinkableArticle[]; onChange: (inform: Inform) => void }) {
    return (
        <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-sm font-medium">
                Instruction
                <Textarea
                    placeholder="What the agent does with these points"
                    value={inform.instruction}
                    onChange={(event) => {
                        onChange({ ...inform, instruction: event.target.value });
                    }}
                />
            </label>
            <PointsForm
                points={inform.points ?? {}}
                onChange={(points) => {
                    onChange({ ...inform, points });
                }}
            />
            <ContextForm
                context={inform.context}
                articles={articles}
                onChange={(context) => {
                    onChange({ ...inform, context });
                }}
            />
        </div>
    );
}

function MappingRows({
    kind,
    title,
    fields,
    mapped,
    facts,
    onChange,
}: {
    kind: "with" | "results";
    title: string;
    fields: NonNullable<ActionView["parameters"]>;
    mapped: Record<string, string>;
    facts: Fact[];
    onChange: (mapped: Record<string, string>) => void;
}) {
    const names = Object.keys(fields);
    if (names.length === 0) return null;
    return (
        <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium">{title}</legend>
            {names.map((name) => {
                const picked = mapped[name] ?? "";
                const offered = factsThatFit(kind, fields[name] ?? {}, facts).map((fact) => fact.name);
                // Why: a fact the step already names stays visible even when it no longer fits, so opening a procedure never hides what is stored.
                const options = picked === "" || offered.includes(picked) ? offered : [...offered, picked];
                return (
                    <label key={name} className="flex items-center gap-1 text-sm">
                        <span className="w-28 shrink-0 truncate" title={name}>
                            {name}
                        </span>
                        <Select
                            aria-label={`${title}: ${name}`}
                            className="flex-1"
                            value={picked}
                            onChange={(event) => {
                                const { [name]: _, ...rest } = mapped;
                                onChange(event.target.value === "" ? rest : { ...rest, [name]: event.target.value });
                            }}
                        >
                            <option value="">{kind === "with" ? "(choose a fact)" : "(ignored)"}</option>
                            {options.map((option) => (
                                <option key={option} value={option}>
                                    {option}
                                </option>
                            ))}
                        </Select>
                    </label>
                );
            })}
        </fieldset>
    );
}

function ActionStepForm({
    name,
    step,
    draft,
    facts,
    actions,
    edit,
}: {
    name: string;
    step: Step;
    draft: Draft;
    facts: Fact[];
    actions: ActionView[];
    edit: (change: (draft: Draft) => Draft) => void;
}) {
    const chosen = actions.find((action) => action.id === step.action);
    const ids = actions.map((action) => action.id);
    const options = step.action === undefined || step.action === "" || ids.includes(step.action) ? ids : [...ids, step.action];
    return (
        <>
            <label className="flex flex-col gap-1 text-sm font-medium">
                Action
                <Select
                    aria-label="Action"
                    value={step.action ?? ""}
                    onChange={(event) => {
                        edit((current) => setBlock(current, name, { action: event.target.value }));
                    }}
                >
                    <option value="">(choose an action)</option>
                    {options.map((id) => (
                        <option key={id} value={id}>
                            {id}
                        </option>
                    ))}
                </Select>
            </label>
            <MappingRows
                kind="with"
                title="Parameters"
                fields={chosen?.parameters ?? {}}
                mapped={step.with ?? {}}
                facts={facts}
                onChange={(mapped) => {
                    edit((current) => setBlock(current, name, { with: mapped }));
                }}
            />
            <MappingRows
                kind="results"
                title="Results"
                fields={chosen?.results ?? {}}
                mapped={step.results ?? {}}
                facts={facts}
                onChange={(mapped) => {
                    edit((current) => setBlock(current, name, { results: mapped }));
                }}
            />
            <TargetSelect
                label="On failure, go to"
                value={step.onFailure ?? ""}
                draft={draft}
                onChange={(to) => {
                    edit((current) => (to === "" ? disconnect(current, name, FAILURE) : connect(current, name, FAILURE, to)));
                }}
            />
        </>
    );
}

/** The form of the selected step: its name, its blocks and its next. Every control writes into the draft; none of them judges it. */
export function StepForm({
    bot,
    id,
    name,
    draft,
    facts,
    procedures,
    articles,
    actions,
    edit,
    onRenamed,
    onDeleted,
    onLeave,
}: {
    bot: string;
    id: string;
    name: string;
    draft: Draft;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    actions: ActionView[];
    edit: (change: (draft: Draft) => Draft) => void;
    onRenamed: (name: string) => void;
    onDeleted: () => void;
    onLeave: (href: string) => void;
}) {
    const step = draft.steps[name];
    const [typed, setTyped] = useState(name);
    if (step === undefined) return null;
    const taken = typed !== name && typed in draft.steps;
    const context = { draft, step: name, facts, edit };
    return (
        <section aria-label={`Step ${name}`} className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">Step: {name}</h2>
            <label
                className="flex flex-col gap-1 text-sm"
                title="The step's name, used by next and firstStep. camelCase, unique in the procedure."
            >
                Name ⓘ
                <Input
                    value={typed}
                    aria-invalid={taken || undefined}
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
                {taken && <span className="text-destructive">Another step already has this name.</span>}
            </label>
            <label className="flex items-center gap-2 text-sm" title="The procedure starts here. One step at a time carries the mark.">
                <input
                    type="radio"
                    className="accent-primary"
                    name="first-step"
                    checked={draft.firstStep === name}
                    onChange={() => {
                        edit((current) => markFirst(current, name));
                    }}
                />
                First step ⓘ
            </label>
            {step.listen && <p className="text-sm text-muted-foreground">listen: waits for the customer to write.</p>}
            {step.collect && (
                <CollectForm
                    picked={step.collect}
                    facts={facts}
                    bot={bot}
                    onLeave={onLeave}
                    onChange={(collect) => {
                        edit((current) => setBlock(current, name, { collect }));
                    }}
                />
            )}
            {step.say !== undefined && (
                <label className="flex flex-col gap-1 text-sm font-medium">
                    Say
                    <Textarea
                        value={step.say}
                        onChange={(event) => {
                            edit((current) => setBlock(current, name, { say: event.target.value }));
                        }}
                    />
                </label>
            )}
            {step.assign && (
                <AssignForm
                    assigned={step.assign}
                    facts={facts}
                    bot={bot}
                    onLeave={onLeave}
                    onChange={(assign) => {
                        edit((current) => setBlock(current, name, { assign }));
                    }}
                />
            )}
            {step.start !== undefined && (
                <StartForm
                    target={step.start}
                    offered={startable(procedures, id)}
                    onChange={(start) => {
                        edit((current) => setBlock(current, name, { start }));
                    }}
                />
            )}
            {step.inform && (
                <InformForm
                    inform={step.inform}
                    articles={articles}
                    onChange={(inform) => {
                        edit((current) => setBlock(current, name, { inform }));
                    }}
                />
            )}
            {step.action !== undefined && (
                <ActionStepForm name={name} step={step} draft={draft} facts={facts} actions={actions} edit={edit} />
            )}
            <div className="flex flex-col gap-2">
                <h3 className="text-base font-semibold">Next</h3>
                {Array.isArray(step.next) ? (
                    <RuleEditor rules={step.next} context={context} />
                ) : (
                    <TargetSelect
                        label="Go to"
                        value={step.next ?? ""}
                        draft={draft}
                        onChange={(to) => {
                            edit((current) => (to === "" ? disconnect(current, name, "next") : connect(current, name, "next", to)));
                        }}
                    />
                )}
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        edit((current) => addRule(current, name));
                    }}
                >
                    Add rule
                </Button>
            </div>
            <Button
                type="button"
                variant="destructive"
                size="sm"
                onClick={() => {
                    edit((current) => deleteStep(current, name));
                    onDeleted();
                }}
            >
                Delete step
            </Button>
        </section>
    );
}
