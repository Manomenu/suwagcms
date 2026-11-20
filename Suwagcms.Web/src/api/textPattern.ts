import type { components } from "./openapi";

export type TextPattern = components["schemas"]["TextPattern"];

export interface PatternForm {
    patternRegex: string;
    patternMeans: string;
    patternExample: string;
    patternTellCustomer: string;
}

export const NO_PATTERN: PatternForm = { patternRegex: "", patternMeans: "", patternExample: "", patternTellCustomer: "upfront" };

export function patternFormOf(pattern: TextPattern | null | undefined): PatternForm {
    if (pattern == null) return NO_PATTERN;
    return {
        patternRegex: pattern.regex ?? "",
        patternMeans: pattern.means ?? "",
        patternExample: pattern.example ?? "",
        patternTellCustomer: pattern.tellCustomer ?? "upfront",
    };
}

export function patternOf(form: PatternForm): TextPattern | null {
    const example = form.patternExample.trim() === "" ? {} : { example: form.patternExample };
    if (form.patternRegex.trim() === "" && form.patternMeans.trim() === "" && form.patternExample.trim() === "") return null;
    return { regex: form.patternRegex, means: form.patternMeans, ...example, tellCustomer: form.patternTellCustomer };
}
