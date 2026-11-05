import type { Mistake } from "@/api/client";

import type { Persona, SavePersona } from "./api";

// Why: the form holds each text exactly as written, so a refusal never loses a keystroke.
export interface PersonaForm {
    personality: string;
    inScope: string;
    outOfScope: string;
    whenOutOfScope: string;
}

export type FieldName = keyof PersonaForm;

// Why: a person has to learn from the page what each section is for, and a tooltip says it without crowding the page.
export const HINTS = {
    personality: "How the bot talks: its voice and manner. A version carries this text to the bot.",
    scope: "What the bot talks about, what it does not, and how it answers a request outside that. A version carries these texts to the bot.",
    inScope: "What the bot talks about.",
    outOfScope: "What the bot does not talk about.",
    whenOutOfScope: "How the bot answers a request it does not talk about.",
} as const;

const FIELDS: Record<string, FieldName> = {
    personality: "personality",
    "scope.inScope": "inScope",
    "scope.outOfScope": "outOfScope",
    "scope.whenOutOfScope": "whenOutOfScope",
};

export function formOf(persona: Persona): PersonaForm {
    return {
        personality: persona.personality ?? "",
        inScope: persona.scope?.inScope ?? "",
        outOfScope: persona.scope?.outOfScope ?? "",
        whenOutOfScope: persona.scope?.whenOutOfScope ?? "",
    };
}

// Why: which text counts as empty is the server's to say, so every text goes as typed.
export function bodyOf(form: PersonaForm): SavePersona {
    return {
        personality: form.personality,
        scope: { inScope: form.inScope, outOfScope: form.outOfScope, whenOutOfScope: form.whenOutOfScope },
    };
}

export function isEmpty(persona: Persona): boolean {
    return persona.personality === null && persona.scope === null;
}

export function isDirty(form: PersonaForm, saved: PersonaForm): boolean {
    return (Object.keys(form) as FieldName[]).some((name) => form[name] !== saved[name]);
}

// Why: null is for what no one field can fix, such as an out-of-date revision, which the page shows above the fields.
export function fieldOf(mistake: Mistake): FieldName | null {
    return FIELDS[mistake.path] ?? null;
}
