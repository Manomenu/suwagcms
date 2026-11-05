import type { Mistake } from "@/api/client";

import { implementationOf, type ActionField, type ActionView, type SaveAction } from "./api";

export type ListName = "parameters" | "results";

/** One parameter or result as typed, as text, so that a refusal never loses a keystroke. */
export interface FieldRow {
    name: string;
    type: string;
    description: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

export interface HeaderRow {
    name: string;
    value: string;
}

export interface ActionForm {
    id: string;
    description: string;
    method: string;
    url: string;
    timeout: string;
    headers: HeaderRow[];
    parameters: FieldRow[];
    results: FieldRow[];
}

export const METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"] as const;

export function emptyRow(): FieldRow {
    return { name: "", type: "text", description: "", values: "", min: "", max: "", maxLength: "" };
}

export function emptyForm(): ActionForm {
    return { id: "", description: "", method: "POST", url: "", timeout: "", headers: [], parameters: [], results: [] };
}

function rowsOf(fields: Record<string, ActionField> | null): FieldRow[] {
    return Object.entries(fields ?? {}).map(([name, field]) => ({
        name,
        type: field.type ?? "",
        description: field.description ?? "",
        values: (field.values ?? []).join(", "),
        min: field.min == null ? "" : String(field.min),
        max: field.max == null ? "" : String(field.max),
        maxLength: field.maxLength == null ? "" : String(field.maxLength),
    }));
}

export function formOf(action: ActionView): ActionForm {
    const implementation = implementationOf(action);
    const http = implementation !== null && implementation !== "code" ? implementation.http : null;
    return {
        id: action.id,
        description: action.description,
        method: http?.method ?? "POST",
        url: http?.url ?? "",
        timeout: http?.timeoutSeconds == null ? "" : String(http.timeoutSeconds),
        headers: Object.entries(http?.headers ?? {}).map(([name, value]) => ({ name, value })),
        parameters: rowsOf(action.parameters),
        results: rowsOf(action.results),
    };
}

function whole(text: string): number | null {
    const trimmed = text.trim();
    if (trimmed === "") return null;
    const number = Number(trimmed);
    return Number.isNaN(number) ? null : number;
}

function fieldsOf(rows: FieldRow[]): Record<string, ActionField> | null {
    if (rows.length === 0) return null;
    return Object.fromEntries(
        rows.map((row) => [
            row.name,
            {
                type: row.type,
                description: row.description,
                values: row.type === "enum" ? row.values.split(",").map((value) => value.trim()) : null,
                min: row.type === "int" ? whole(row.min) : null,
                max: row.type === "int" ? whole(row.max) : null,
                maxLength: row.type === "text" ? whole(row.maxLength) : null,
            },
        ]),
    );
}

/** Only the constraints of each row's type are sent: the others are hidden, and a constraint of another type is a mistake. */
export function bodyOf(form: ActionForm): SaveAction {
    return {
        description: form.description,
        parameters: fieldsOf(form.parameters),
        results: fieldsOf(form.results),
        implementation: {
            http: {
                method: form.method,
                url: form.url,
                headers: form.headers.length === 0 ? null : Object.fromEntries(form.headers.map((header) => [header.name, header.value])),
                timeoutSeconds: whole(form.timeout),
            },
        },
    };
}

/** Where a mistake shows: the field its path names (`values[1]` is `values`); a resource that already exists shows beside the id. */
export function placeOf(mistake: Mistake): string {
    return mistake.code === "resource.exists" ? "id" : mistake.path.replace(/\[\d+\]$/, "");
}

/** Every place the open form shows a mistake; a mistake with any other place is listed apart, so none is lost. */
export function placesOf(form: ActionForm): string[] {
    const http = "implementation.http";
    const lists = (["parameters", "results"] as const).flatMap((list) =>
        form[list].flatMap((row) => {
            const at = `${list}.${row.name}`;
            return [at, `${at}.type`, `${at}.description`, `${at}.values`, `${at}.min`, `${at}.max`, `${at}.maxLength`];
        }),
    );
    return [
        "id",
        "description",
        `${http}.method`,
        `${http}.url`,
        `${http}.timeoutSeconds`,
        ...form.headers.map((header) => `${http}.headers.${header.name}`),
        ...lists,
    ];
}

export function constraintOf(field: ActionField): string {
    if (field.type === "enum") return `enum: ${(field.values ?? []).join(", ")}`;
    if (field.type === "int") return `int, ${field.min ?? "?"} to ${field.max ?? "?"}`;
    if (field.type === "text") return `text, up to ${field.maxLength ?? "?"}`;
    return field.type ?? "";
}
