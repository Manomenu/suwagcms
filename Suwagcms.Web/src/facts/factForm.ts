import type { Mistake } from "@/api/client";
import { whole } from "@/api/constraints";

import { NO_PATTERN, patternFormOf, patternOf, type PatternForm } from "@/api/textPattern";

import type { Fact, SaveFact } from "./api";

export type FieldName =
    | "name"
    | "type"
    | "description"
    | "writableBy"
    | "values"
    | "min"
    | "max"
    | "maxLength"
    | "pattern.regex"
    | "pattern.means"
    | "pattern.example"
    | "pattern.tellCustomer";

/** What the person typed, as text, so that a refusal never loses a keystroke. */
export interface FactForm extends PatternForm {
    name: string;
    type: string;
    description: string;
    writableBy: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

const FIELDS: readonly FieldName[] = [
    "name",
    "type",
    "description",
    "writableBy",
    "values",
    "min",
    "max",
    "maxLength",
    "pattern.regex",
    "pattern.means",
    "pattern.example",
    "pattern.tellCustomer",
];

export function emptyForm(): FactForm {
    return { name: "", type: "text", description: "", writableBy: "set", values: "", min: "", max: "", maxLength: "", ...NO_PATTERN };
}

export function formOf(fact: Fact): FactForm {
    return {
        name: fact.name,
        type: fact.type,
        description: fact.description,
        writableBy: fact.writableBy,
        values: (fact.values ?? []).join(", "),
        min: fact.min === null ? "" : String(fact.min),
        max: fact.max === null ? "" : String(fact.max),
        maxLength: fact.maxLength === null ? "" : String(fact.maxLength),
        ...patternFormOf(fact.pattern),
    };
}

/** Only the constraints of the chosen type are sent: the others are hidden, and a constraint of another type is a mistake. */
export function bodyOf(form: FactForm): SaveFact {
    return {
        type: form.type,
        description: form.description,
        writableBy: form.writableBy,
        values: form.type === "enum" ? form.values.split(",").map((value) => value.trim()) : null,
        min: form.type === "int" ? whole(form.min) : null,
        max: form.type === "int" ? whole(form.max) : null,
        maxLength: form.type === "text" ? whole(form.maxLength) : null,
        pattern: form.type === "text" ? patternOf(form) : null,
    };
}

/** The field a mistake belongs beside: its path names it (`values[1]` is `values`); null when no field does. */
export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "name";
    const root = mistake.path.replace(/\[\d+\]$/, "");
    return FIELDS.find((field) => field === root) ?? null;
}

/** A place inside a procedure as an author finds it: the step and the block; any other place as the server names it. */
export function referrerPlace(location: string): string {
    const match = /^steps\.([^.]+)\.([^.[]+)/.exec(location);
    if (match === null) return location;
    return `step ${match[1] ?? ""} (${match[2] === "next" ? "when" : (match[2] ?? "")})`;
}

export { constraintText as constraintOf } from "@/api/constraints";
