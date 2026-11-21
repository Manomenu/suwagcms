import { describe, expect, it } from "vitest";

import type { Mistake } from "@/api/client";

import { bodyOf, fieldOf, formOf, isDirty, isEmpty } from "./personaForm";

function mistakeAt(path: string, code: Mistake["code"] = "persona.text.tooLong"): Mistake {
    return { code, stage: "Save", resource: { kind: "Persona", id: "persona" }, path, message: "m", fix: "f", related: [] };
}

describe("personaForm", () => {
    it("AC-bot-persona-1: every text goes to the server exactly as typed, line breaks and spaces included", () => {
        const typed = { personality: "  One\n\nTwo  \n", inScope: "In\nscope", outOfScope: "\n", whenOutOfScope: "" };

        expect(bodyOf(typed)).toEqual({
            personality: "  One\n\nTwo  \n",
            scope: { inScope: "In\nscope", outOfScope: "\n", whenOutOfScope: "" },
        });
    });

    it("AC-bot-persona-1: a form opens on the texts as they are stored", () => {
        const form = formOf({ personality: "One\nTwo\n", scope: { inScope: "In", outOfScope: null, whenOutOfScope: null }, revision: 3 });

        expect(form).toEqual({ personality: "One\nTwo\n", inScope: "In", outOfScope: "", whenOutOfScope: "" });
        expect(isDirty(form, form)).toBe(false);
        expect(isDirty({ ...form, inScope: "In\n" }, form)).toBe(true);
    });

    it("AC-bot-persona-2: a persona with no text is empty, and one with a part of it is not", () => {
        expect(isEmpty({ personality: null, scope: null, revision: 0 })).toBe(true);
        expect(isEmpty({ personality: null, scope: { inScope: "In", outOfScope: null, whenOutOfScope: null }, revision: 1 })).toBe(false);
    });

    it("AC-bot-persona-4: a mistake belongs beside the field its path names", () => {
        expect(fieldOf(mistakeAt("personality"))).toBe("personality");
        expect(fieldOf(mistakeAt("scope.inScope"))).toBe("inScope");
        expect(fieldOf(mistakeAt("scope.outOfScope"))).toBe("outOfScope");
        expect(fieldOf(mistakeAt("scope.whenOutOfScope"))).toBe("whenOutOfScope");
    });

    it("AC-bot-persona-6: an out-of-date revision belongs to no one field", () => {
        expect(fieldOf(mistakeAt("revision", "resource.stale"))).toBeNull();
    });
});
