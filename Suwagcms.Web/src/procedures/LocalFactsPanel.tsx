import { Button, Field, InfoTip, Input, Select } from "@maniumek/suwfrontkit";
import { useState, type ReactNode } from "react";

import { constraintText, whole } from "@/api/constraints";
import { NO_PATTERN, patternFormOf, patternOf, type PatternForm } from "@/api/textPattern";
import { ConstraintFields, PatternFields } from "@/design/constraintFields";
import { FACT_HINTS } from "@/design/factHints";

import { PROCEDURE_HINTS } from "./hints";
import { removeLocalFact, setLocalFact, type Draft, type LocalFact } from "./draft";

interface Typed extends PatternForm {
    name: string;
    type: string;
    description: string;
    writableBy: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

const EMPTY: Typed = {
    name: "",
    type: "text",
    description: "",
    writableBy: "set",
    values: "",
    min: "",
    max: "",
    maxLength: "",
    ...NO_PATTERN,
};

function typedOf(name: string, fact: LocalFact): Typed {
    return {
        name,
        type: fact.type ?? "text",
        description: fact.description ?? "",
        writableBy: fact.writableBy ?? "set",
        values: (fact.values ?? []).join(", "),
        min: fact.min == null ? "" : String(fact.min),
        max: fact.max == null ? "" : String(fact.max),
        maxLength: fact.maxLength == null ? "" : String(fact.maxLength),
        ...patternFormOf(fact.pattern),
    };
}

function factOf(typed: Typed): LocalFact {
    const pattern = typed.type === "text" ? patternOf(typed) : null;
    return {
        type: typed.type,
        description: typed.description,
        writableBy: typed.writableBy,
        ...(typed.type === "enum" ? { values: typed.values.split(",").map((value) => value.trim()) } : {}),
        ...(typed.type === "int" ? { min: whole(typed.min), max: whole(typed.max) } : {}),
        ...(typed.type === "text" ? { maxLength: whole(typed.maxLength) } : {}),
        ...(pattern === null ? {} : { pattern }),
    };
}

function summaryOf(fact: LocalFact): string {
    return `${constraintText({ ...fact, type: fact.type ?? "text" })} · ${fact.writableBy ?? "set"}`;
}

const field = (name: string, label: string, hint: string, control: (props: object) => ReactNode) => (
    <Field prefix="local-fact" name={name} label={label} hint={hint} errors={[]}>
        {control}
    </Field>
);

function TextInput({
    name,
    label,
    hint,
    value,
    onChange,
}: {
    name: string;
    label: string;
    hint: string;
    value: string;
    onChange: (value: string) => void;
}) {
    return field(name, label, hint, (props) => (
        <Input
            {...props}
            value={value}
            onChange={(event) => {
                onChange(event.target.value);
            }}
        />
    ));
}

function LocalFactForm({
    initial,
    editing,
    onDone,
    onCancel,
}: {
    initial: Typed;
    editing: boolean;
    onDone: (name: string, fact: LocalFact) => void;
    onCancel: () => void;
}) {
    const [typed, setTyped] = useState(initial);
    const change = (part: Partial<Typed>) => {
        setTyped((before) => ({ ...before, ...part }));
    };
    return (
        <div role="group" aria-label="Local fact" className="flex flex-col gap-2 rounded-md border border-border p-2">
            {!editing && (
                <TextInput
                    name="name"
                    label="Name"
                    hint={PROCEDURE_HINTS.localFactName}
                    value={typed.name}
                    onChange={(name) => {
                        change({ name });
                    }}
                />
            )}
            {field("type", "Type", FACT_HINTS.type, (props) => (
                <Select
                    {...props}
                    className="w-full"
                    value={typed.type}
                    onChange={(event) => {
                        change({ type: event.target.value });
                    }}
                >
                    <option value="text">text</option>
                    <option value="enum">enum</option>
                    <option value="int">int</option>
                </Select>
            ))}
            <ConstraintFields form={typed} field={field} onChange={change} />
            {typed.type === "text" && <PatternFields form={typed} field={field} onChange={change} />}
            <TextInput
                name="description"
                label="Fact description"
                hint={FACT_HINTS.description}
                value={typed.description}
                onChange={(description) => {
                    change({ description });
                }}
            />
            {field("writableBy", "Writable by", FACT_HINTS.writableBy, (props) => (
                <Select
                    {...props}
                    className="w-full"
                    value={typed.writableBy}
                    onChange={(event) => {
                        change({ writableBy: event.target.value });
                    }}
                >
                    <option value="assign">assign</option>
                    <option value="set">set</option>
                    <option value="correct">correct</option>
                </Select>
            ))}
            <div className="flex gap-2">
                <Button
                    type="button"
                    size="sm"
                    disabled={typed.name.trim() === ""}
                    onClick={() => {
                        onDone(typed.name.trim(), factOf(typed));
                    }}
                >
                    Keep
                </Button>
                <Button type="button" size="sm" variant="outline" onClick={onCancel}>
                    Cancel
                </Button>
            </div>
        </div>
    );
}

export function LocalFactsPanel({ draft, edit }: { draft: Draft; edit: (change: (draft: Draft) => Draft) => void }) {
    const [open, setOpen] = useState<string | null>(null);
    const adding = open === "";
    const entries = Object.entries(draft.localFacts);
    const done = (name: string, fact: LocalFact) => {
        edit((current) => setLocalFact(current, name, fact));
        setOpen(null);
    };
    const cancel = () => {
        setOpen(null);
    };
    return (
        <section aria-label="Local facts" className="flex flex-col gap-2">
            <div className="flex items-center justify-between gap-2">
                <h3 className="flex items-center gap-1 text-sm font-semibold">
                    Local facts
                    <InfoTip about="Local facts" help={PROCEDURE_HINTS.localFacts} />
                </h3>
                <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => {
                        setOpen("");
                    }}
                >
                    Add a local fact
                </Button>
            </div>
            {entries.length === 0 && !adding && <p className="text-sm text-muted-foreground">Only this procedure would see them.</p>}
            <ul className="flex flex-col gap-2">
                {entries.map(([name, fact]) => (
                    <li key={name} className="flex flex-col gap-1 text-sm">
                        <div className="flex items-center gap-2">
                            <span className="min-w-0 flex-1 truncate font-medium" title={name}>
                                {name}
                            </span>
                            <Button
                                type="button"
                                size="xs"
                                variant="outline"
                                aria-label={`Edit ${name}`}
                                onClick={() => {
                                    setOpen(name);
                                }}
                            >
                                Edit
                            </Button>
                            <Button
                                type="button"
                                size="xs"
                                variant="outline"
                                aria-label={`Remove ${name}`}
                                onClick={() => {
                                    edit((current) => removeLocalFact(current, name));
                                }}
                            >
                                Remove
                            </Button>
                        </div>
                        <span className="text-muted-foreground">{summaryOf(fact)}</span>
                        {open === name && (
                            <LocalFactForm key={name} initial={typedOf(name, fact)} editing onDone={done} onCancel={cancel} />
                        )}
                    </li>
                ))}
            </ul>
            {adding && <LocalFactForm initial={EMPTY} editing={false} onDone={done} onCancel={cancel} />}
        </section>
    );
}
