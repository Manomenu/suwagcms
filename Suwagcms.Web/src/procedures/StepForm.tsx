import { useState } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { Fact } from "./api";
import {
    addRule,
    articleLabel,
    collectable,
    connect,
    disconnect,
    deleteStep,
    markFirst,
    renameStep,
    setBlock,
    startable,
    valuesOffered,
    withStored,
    type Draft,
    type Inform,
    type LinkableArticle,
} from "./draft";
import { Checklist } from "./Checklist";
import { ContextForm } from "./ContextForm";
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

function InformForm({
    inform,
    articles,
    facts,
    onChange,
}: {
    inform: Inform;
    articles: LinkableArticle[];
    facts: Fact[];
    onChange: (inform: Inform) => void;
}) {
    const articleNames = withStored(
        articles.map((article) => article.id),
        inform.articles ?? [],
    );
    const factNames = withStored(
        facts.map((fact) => fact.name),
        inform.facts ?? [],
    );
    return (
        <div className="flex flex-col gap-3">
            <label className="flex flex-col gap-1 text-sm font-medium">
                Instruction
                <Textarea
                    placeholder="How to pass this on"
                    value={inform.instruction}
                    onChange={(event) => {
                        onChange({ ...inform, instruction: event.target.value });
                    }}
                />
            </label>
            {articleNames.length === 0 ? (
                <p className="text-sm text-muted-foreground">This bot has no articles yet.</p>
            ) : (
                <Checklist
                    legend="Articles to pass on"
                    names={articleNames}
                    picked={inform.articles ?? []}
                    labelOf={(id) => articleLabel(articles, id)}
                    onChange={(picked) => {
                        onChange({ ...inform, articles: picked });
                    }}
                />
            )}
            {factNames.length === 0 ? (
                <p className="text-sm text-muted-foreground">This bot has no facts yet.</p>
            ) : (
                <Checklist
                    legend="Facts to pass on"
                    names={factNames}
                    picked={inform.facts ?? []}
                    onChange={(picked) => {
                        onChange({ ...inform, facts: picked });
                    }}
                />
            )}
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

/** The form of the selected step: its name, its blocks and its next. Every control writes into the draft; none of them judges it. */
export function StepForm({
    bot,
    id,
    name,
    draft,
    facts,
    procedures,
    articles,
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
                    facts={facts}
                    onChange={(inform) => {
                        edit((current) => setBlock(current, name, { inform }));
                    }}
                />
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
