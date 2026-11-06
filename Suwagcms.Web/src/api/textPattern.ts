// A text fact's pattern as a form holds it, shared by the form of a global fact and of a local one.
import type { components } from "./openapi";

export type TextPattern = components["schemas"]["TextPattern"];

/** A pattern as typed. Regex, means and example all empty is no pattern, so emptying them is how a person clears one. */
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
