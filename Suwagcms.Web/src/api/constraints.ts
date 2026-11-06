/** What a person typed in a number's field: null when empty or not a number, which the server then reports. */
export function whole(text: string): number | null {
    const trimmed = text.trim();
    if (trimmed === "") return null;
    const number = Number(trimmed);
    return Number.isNaN(number) ? null : number;
}

/** What a fact, a local fact or an action's field allows, in words. */
export function constraintText(field: {
    type?: string | null;
    values?: string[] | null;
    min?: number | string | null;
    max?: number | string | null;
    maxLength?: number | string | null;
    pattern?: { regex?: string | null } | null;
}): string {
    if (field.type === "enum") return `enum: ${(field.values ?? []).join(", ")}`;
    if (field.type === "int") return `int, ${field.min ?? "?"} to ${field.max ?? "?"}`;
    if (field.type === "text")
        return `text, up to ${field.maxLength ?? "?"}${field.pattern == null ? "" : `, pattern ${field.pattern.regex ?? ""}`}`;
    return field.type ?? "";
}
