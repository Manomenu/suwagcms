import { useState } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { ActionView, Fact } from "./api";
import {
    addRule,
    collectable,
    connect,
    disconnect,
    deleteStep,
    FAILURE,
    factsThatFit,
    markFirst,
    renameStep,
    setBlock,
    valuesOffered,
    type Draft,
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
    // Why: a fact the step already names stays visible even when it is not offered, so opening a procedure never hides what is stored.
    const names = [...offered.map((fact) => fact.name), ...picked.filter((name) => !offered.some((fact) => fact.name === name))];
    if (names.length === 0) return <NoFacts bot={bot} onLeave={onLeave} />;
    return (
        <fieldset className="flex flex-col gap-1">
            <legend className="text-sm font-medium">Collect these facts</legend>
            {names.map((name) => (
                <label key={name} className="flex items-center gap-2 text-sm">
                    <input
                        type="checkbox"
                        className="accent-primary"
                        checked={picked.includes(name)}
                        onChange={(event) => {
                            onChange(event.target.checked ? [...picked, name] : picked.filter((other) => other !== name));
                        }}
                    />
                    {name}
                </label>
            ))}
        </fieldset>
    );
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
    name,
    draft,
    facts,
    actions,
    edit,
    onRenamed,
    onDeleted,
    onLeave,
}: {
    bot: string;
    name: string;
    draft: Draft;
    facts: Fact[];
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
