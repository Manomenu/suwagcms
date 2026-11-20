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

export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "name";
    const root = mistake.path.replace(/\[\d+\]$/, "");
    return FIELDS.find((field) => field === root) ?? null;
}

export function referrerPlace(location: string): string {
    const match = /^steps\.([^.]+)\.([^.[]+)/.exec(location);
    if (match === null) return location;
    return `step ${match[1] ?? ""} (${match[2] === "next" ? "condition" : (match[2] ?? "")})`;
}

export { constraintText as constraintOf } from "@/api/constraints";
