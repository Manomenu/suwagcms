import { useState } from "react";

import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import { removeLocalFact, setLocalFact, type Draft, type LocalFact } from "./draft";

interface Typed {
    name: string;
    type: string;
    description: string;
    writableBy: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

const EMPTY: Typed = { name: "", type: "text", description: "", writableBy: "set", values: "", min: "", max: "", maxLength: "" };

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
    };
}

function whole(text: string): number | null {
    const number = Number(text.trim());
    return text.trim() === "" || Number.isNaN(number) ? null : number;
}

// Why: only the constraints of the chosen type are sent, since a constraint of another type is a mistake.
function factOf(typed: Typed): LocalFact {
    return {
        type: typed.type,
        description: typed.description,
        writableBy: typed.writableBy,
        ...(typed.type === "enum" ? { values: typed.values.split(",").map((value) => value.trim()) } : {}),
        ...(typed.type === "int" ? { min: whole(typed.min), max: whole(typed.max) } : {}),
        ...(typed.type === "text" ? { maxLength: whole(typed.maxLength) } : {}),
    };
}

function summaryOf(fact: LocalFact): string {
    const limits =
        fact.type === "enum"
            ? `enum: ${(fact.values ?? []).join(", ")}`
            : fact.type === "int"
              ? `int, ${fact.min ?? "?"} to ${fact.max ?? "?"}`
              : `text, up to ${fact.maxLength ?? "?"}`;
    return `${limits} · ${fact.writableBy ?? "set"}`;
}

function TextInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
    return (
        <label className="flex flex-col gap-1 text-sm">
            {label}
            <Input
                value={value}
                onChange={(event) => {
                    onChange(event.target.value);
                }}
            />
        </label>
    );
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
                    label="Name"
                    value={typed.name}
                    onChange={(name) => {
                        change({ name });
                    }}
                />
            )}
            <label className="flex flex-col gap-1 text-sm">
                Type
                <Select
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
            </label>
            {typed.type === "enum" && (
                <TextInput
                    label="Values (separated by commas)"
                    value={typed.values}
                    onChange={(values) => {
                        change({ values });
                    }}
                />
            )}
            {typed.type === "int" && (
                <>
                    <TextInput
                        label="Minimum"
                        value={typed.min}
                        onChange={(min) => {
                            change({ min });
                        }}
                    />
                    <TextInput
                        label="Maximum"
                        value={typed.max}
                        onChange={(max) => {
                            change({ max });
                        }}
                    />
                </>
            )}
            {typed.type === "text" && (
                <TextInput
                    label="Maximum length"
                    value={typed.maxLength}
                    onChange={(maxLength) => {
                        change({ maxLength });
                    }}
                />
            )}
            <TextInput
                label="Fact description"
                value={typed.description}
                onChange={(description) => {
                    change({ description });
                }}
            />
            <label className="flex flex-col gap-1 text-sm">
                Writable by
                <Select
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
            </label>
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

/** The procedure's own facts: they change the draft like any other edit, so the live list of mistakes judges them and Save stores them. */
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
                <h3 className="text-sm font-semibold">Local facts</h3>
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
