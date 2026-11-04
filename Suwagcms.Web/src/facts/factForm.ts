import type { Mistake } from "@/api/client";

import type { Fact, SaveFact } from "./api";

export type FieldName = "name" | "type" | "description" | "writableBy" | "values" | "min" | "max" | "maxLength";

/** What the person typed, as text, so that a refusal never loses a keystroke. */
export interface FactForm {
    name: string;
    type: string;
    description: string;
    writableBy: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

const FIELDS: readonly FieldName[] = ["name", "type", "description", "writableBy", "values", "min", "max", "maxLength"];

export function emptyForm(): FactForm {
    return { name: "", type: "text", description: "", writableBy: "set", values: "", min: "", max: "", maxLength: "" };
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
    };
}

function whole(text: string): number | null {
    const trimmed = text.trim();
    if (trimmed === "") return null;
    const number = Number(trimmed);
    return Number.isNaN(number) ? null : number;
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
    };
}

/** The field a mistake belongs beside: its path names it (`values[1]` is `values`); null when no field does. */
export function fieldOf(mistake: Mistake): FieldName | null {
    if (mistake.code === "resource.exists") return "name";
    const root = mistake.path.replace(/\[\d+\]$/, "");
    return FIELDS.find((field) => field === root) ?? null;
}

export function constraintOf(fact: Fact): string {
    if (fact.type === "enum") return `enum: ${(fact.values ?? []).join(", ")}`;
    if (fact.type === "int") return `int, ${fact.min ?? "?"} to ${fact.max ?? "?"}`;
    if (fact.type === "text") return `text, up to ${fact.maxLength ?? "?"}`;
    return fact.type;
}
