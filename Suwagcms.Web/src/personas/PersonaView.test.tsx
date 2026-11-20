import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import type { Mistake } from "@/api/client";

import { HINTS, type PersonaForm } from "./personaForm";
import { PersonaView } from "./PersonaView";

const form: PersonaForm = { personality: "One\nTwo", inScope: "In", outOfScope: "Out", whenOutOfScope: "Decline" };

function mistake(path: string, code: string, message: string): Mistake {
    return { code, stage: "Save", resource: { kind: "Persona", id: "persona" }, path, message, fix: "The fix.", related: [] };
}

function html(overrides: Partial<Parameters<typeof PersonaView>[0]> = {}): string {
    return renderToStaticMarkup(
        <PersonaView
            bot="rzeczoznawca"
            form={form}
            revision={4}
            empty={false}
            saved={false}
            saving={false}
            failure={null}
            mistakes={[]}
            outOfDate={false}
            history={{
                entries: [],
                since: "2026-11-04T08:00:00+00:00",
                failure: null,
                paging: { page: 1, pages: 1, asking: false, failure: null, onMove: () => undefined },
            }}
            onChange={() => undefined}
            onSave={() => undefined}
            onReload={() => undefined}
            {...overrides}
        />,
    );
}

describe("PersonaView", () => {
    it("AC-change-history-20: Persona has exactly one History heading, from the wrapper", () => {
        const history = {
            entries: [],
            since: "2026-11-04T08:00:00+00:00",
            failure: null,
            paging: { page: 1, pages: 1, asking: false, failure: null, onMove: () => undefined },
        };

        expect(html({ history }).match(/<h\d[^>]*>History<\/h\d>/g)).toHaveLength(1);
    });

    it("AC-bot-persona-1: shows the personality and the three parts of the scope as plain text boxes, with Save as the only control", () => {
        const page = html();

        expect(page.match(/<textarea/g)).toHaveLength(4);
        expect(page.match(/<button/g)?.length).toBe(1 + (page.match(/>About /g)?.length ?? 0));
        expect(page).toContain(">Save<");
        expect(page).toContain("revision 4");
        for (const label of ["Personality", "In scope", "Out of scope", "When out of scope"]) expect(page).toContain(label);
        expect(page).toContain("One\nTwo");
    });

    it("AC-bot-persona-1: a tooltip on each section says what it is for", () => {
        const page = html();

        expect(page).toContain(`>${HINTS.personality}<`);
        expect(page).toContain(`>${HINTS.scope}<`);
    });

    it("AC-bot-persona-2: an empty persona says so", () => {
        expect(html({ empty: true })).toContain("This persona is empty");
        expect(html()).not.toContain("This persona is empty");
    });

    it("AC-bot-persona-4: a text above the limit is refused beside its field, and every mistake is shown with its fix", () => {
        const page = html({
            mistakes: [
                mistake("personality", "persona.text.tooLong", "Personality is too long."),
                mistake("scope.outOfScope", "persona.text.tooLong", "Out of scope is too long."),
            ],
        });

        expect(page).toMatch(/id="persona-personality-mistakes"[^>]*><li>Personality is too long\. The fix\.<\/li>/);
        expect(page).toMatch(/id="persona-outOfScope-mistakes"[^>]*><li>Out of scope is too long\. The fix\.<\/li>/);
    });

    it("AC-bot-persona-6: an out-of-date save says so with the current revision, keeps the typed text and offers a reload", () => {
        const page = html({
            outOfDate: true,
            mistakes: [mistake("revision", "resource.stale", "It is now revision 5.")],
        });

        expect(page).toContain("It is now revision 5. The fix.");
        expect(page).toContain("One\nTwo");
        expect(page).toContain("Reload the persona");
    });
});
