import { Link } from "wouter";

import { listHref } from "@/api/resourceAddress";
import { Button } from "@/design/button";
import { Field, Legend } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";
import { Textarea } from "@/design/textarea";

import type { ActionView, Fact } from "./api";
import { Checklist } from "./Checklist";
import {
    addPoint,
    collectable,
    factsThatFit,
    removePoint,
    renamePoint,
    setBlock,
    startable,
    valuesOffered,
    withStored,
    type Draft,
    type Inform,
    type LinkableArticle,
    type LinkableBase,
    type Step,
} from "./draft";
import { PROCEDURE_HINTS } from "./hints";
import { KnowledgeForm, type Beside } from "./KnowledgeForm";

function NoFacts({ bot, onLeave }: { bot: string; onLeave: (href: string) => void }) {
    const href = listHref(bot, "facts");
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
    return <Checklist legend="Collect these facts" hint={PROCEDURE_HINTS.collect} names={names} picked={picked} onChange={onChange} />;
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
            <Legend hint={PROCEDURE_HINTS.assign}>Assign these values</Legend>
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
    const options = offered.includes(target) || target === "" ? offered : [target, ...offered];
    return (
        <Field prefix="step" name="start" label="Start this procedure" hint={PROCEDURE_HINTS.start} errors={[]}>
            {(props) => (
                <Select
                    {...props}
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
            )}
        </Field>
    );
}

function PointsForm({ points, onChange }: { points: Record<string, string>; onChange: (points: Record<string, string>) => void }) {
    const entries = Object.entries(points);
    return (
        <fieldset className="flex flex-col gap-2">
            <Legend hint={PROCEDURE_HINTS.points}>Points the customer must hear</Legend>
            {entries.map(([name, text], index) => (
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

function InformForm({
    name,
    inform,
    showKnowledge,
    bases,
    articles,
    beside,
    onChange,
}: {
    name: string;
    inform: Inform;
    showKnowledge: boolean;
    bases: LinkableBase[];
    articles: LinkableArticle[];
    beside: Beside;
    onChange: (inform: Inform) => void;
}) {
    return (
        <div className="flex flex-col gap-3">
            <Field prefix="step" name="instruction" label="Instruction" hint={PROCEDURE_HINTS.instruction} errors={[]}>
                {(props) => (
                    <Textarea
                        {...props}
                        placeholder="What the agent does with these points"
                        value={inform.instruction}
                        onChange={(event) => {
                            onChange({ ...inform, instruction: event.target.value });
                        }}
                    />
                )}
            </Field>
            <PointsForm
                points={inform.points ?? {}}
                onChange={(points) => {
                    onChange({ ...inform, points });
                }}
            />
            <KnowledgeForm
                owner={name}
                show={showKnowledge}
                knowledge={inform.knowledge ?? []}
                bases={bases}
                articles={articles}
                beside={beside}
                onChange={(knowledge) => {
                    const { knowledge: _, ...rest } = inform;
                    onChange(knowledge.length === 0 ? rest : { ...inform, knowledge });
                }}
            />
        </div>
    );
}

function MappingRows({
    kind,
    title,
    hint,
    fields,
    mapped,
    facts,
    onChange,
}: {
    kind: "with" | "results";
    title: string;
    hint: string;
    fields: NonNullable<ActionView["parameters"]>;
    mapped: Record<string, string>;
    facts: Fact[];
    onChange: (mapped: Record<string, string>) => void;
}) {
    const names = Object.keys(fields);
    if (names.length === 0) return null;
    return (
        <fieldset className="flex flex-col gap-2">
            <Legend hint={hint}>{title}</Legend>
            {names.map((name) => {
                const picked = mapped[name] ?? "";
                const offered = factsThatFit(kind, fields[name] ?? {}, facts).map((fact) => fact.name);
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
    facts,
    actions,
    edit,
}: {
    name: string;
    step: Step;
    facts: Fact[];
    actions: ActionView[];
    edit: (change: (draft: Draft) => Draft) => void;
}) {
    const chosen = actions.find((action) => action.id === step.action);
    const ids = actions.map((action) => action.id);
    const options = step.action === undefined || step.action === "" || ids.includes(step.action) ? ids : [...ids, step.action];
    return (
        <>
            <Field prefix="step" name="action" label="Action" hint={PROCEDURE_HINTS.action} errors={[]}>
                {(props) => (
                    <Select
                        {...props}
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
                )}
            </Field>
            <MappingRows
                kind="with"
                title="Parameters"
                hint={PROCEDURE_HINTS.actionParameters}
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
                hint={PROCEDURE_HINTS.actionResults}
                fields={chosen?.results ?? {}}
                mapped={step.results ?? {}}
                facts={facts}
                onChange={(mapped) => {
                    edit((current) => setBlock(current, name, { results: mapped }));
                }}
            />
        </>
    );
}

export function StepForm({
    bot,
    id,
    name,
    step,
    facts,
    procedures,
    showKnowledge,
    bases,
    articles,
    actions,
    beside,
    edit,
    onLeave,
}: {
    bot: string;
    id: string;
    name: string;
    step: Step;
    facts: Fact[];
    procedures: string[];
    showKnowledge: boolean;
    bases: LinkableBase[];
    articles: LinkableArticle[];
    actions: ActionView[];
    beside: Beside;
    edit: (change: (draft: Draft) => Draft) => void;
    onLeave: (href: string) => void;
}) {
    return (
        <section aria-label={`Step ${name}`} className="flex flex-col gap-3">
            {step.listen && <p className="text-sm text-muted-foreground">Waits for the customer to write.</p>}
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
                <Field prefix="step" name="say" label="Say" hint={PROCEDURE_HINTS.say} errors={[]}>
                    {(props) => (
                        <Textarea
                            {...props}
                            value={step.say}
                            onChange={(event) => {
                                edit((current) => setBlock(current, name, { say: event.target.value }));
                            }}
                        />
                    )}
                </Field>
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
                    name={name}
                    inform={step.inform}
                    showKnowledge={showKnowledge}
                    bases={bases}
                    articles={articles}
                    beside={beside}
                    onChange={(inform) => {
                        edit((current) => setBlock(current, name, { inform }));
                    }}
                />
            )}
            {step.action !== undefined && <ActionStepForm name={name} step={step} facts={facts} actions={actions} edit={edit} />}
        </section>
    );
}
