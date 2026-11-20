import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { fromDetail, type BlockName } from "./draft";
import type { ProcedureDetail } from "./api";
import { ProcedureForm } from "./ProcedureForm";
import { conditionsText } from "./RuleEditor";
import { BLOCK_LABELS, blockOf, rowText } from "./StepsEditor";

const detail = {
    id: "quoteRequest",
    description: "The customer wants a quote.",
    firstStep: "ask",
    steps: {
        ask: { collect: ["property.type"], next: [{ when: { "property.type": { oneOf: ["land"] } }, to: "sorry" }, { to: "tell" }] },
        sorry: { say: "Sorry." },
        tell: { inform: { instruction: "Tell it.", points: { one: "One.", two: "Two." } }, next: "ask" },
    },
    whenStalled: { instruction: "Say so." },
    revision: 1,
    notReady: [],
} as unknown as ProcedureDetail;

function form(over: Partial<Parameters<typeof ProcedureForm>[0]> = {}): string {
    return renderToStaticMarkup(
        <ProcedureForm
            bot="b"
            id="quoteRequest"
            draft={fromDetail(detail)}
            allFacts={[]}
            procedures={[]}
            offered={["listen", "collect", "say"]}
            note={null}
            showKnowledge={false}
            bases={[]}
            articles={[]}
            actions={[]}
            selected={null}
            openConditions={[]}
            onConditionOpen={() => undefined}
            check={{ state: "checking" }}
            dirty={false}
            saving={false}
            canBeInterrupted
            edit={() => undefined}
            onSelect={() => undefined}
            onSave={() => undefined}
            onLeave={() => undefined}
            {...over}
        />,
    );
}

describe("the form of a procedure", () => {
    it("AC-procedure-creator-24: shows Description, Local facts, When stalled, When interrupted, Steps and Save, top to bottom", () => {
        const html = form();
        const order = ["Description", "Local facts", "When stalled", "When interrupted", "Steps", ">Save<"].map((word) =>
            html.indexOf(word),
        );
        expect(order.every((at) => at >= 0)).toBe(true);
        expect([...order].sort((one, other) => one - other)).toEqual(order);
    });

    it("AC-procedure-creator-24: every list and branch ends in an Add step", () => {
        const html = form();
        expect(html).toContain('aria-label="Add step"');
        expect(html).toContain('aria-label="Add step to If of ask"');
        expect(html).toContain('aria-label="Add step to Else of ask"');
    });

    it("AC-procedure-creator-25: a row shows the step's name, editable, its tag and its text, and no next", () => {
        const html = form();
        expect(html).toContain('aria-label="Name of step ask"');
        expect(html).toContain('value="ask"');
        expect(html).toContain(">Ask<");
        expect(html).toContain(">property.type<");
        expect(html).toContain(">Tell<");
        expect(html.split('aria-label="Next step"')).toHaveLength(2);
    });

    it("AC-procedure-creator-25: every block has its tag and its text", () => {
        const steps: [Parameters<typeof blockOf>[0], BlockName, string][] = [
            [{ listen: true }, "listen", "Wait for the next message"],
            [{ collect: ["a.b", "c.d"] }, "collect", "a.b, c.d"],
            [{ say: "Hi" }, "say", "Hi"],
            [{ assign: { "a.b": "c" } }, "assign", "a.b = c"],
            [{ inform: { instruction: "Tell.", points: { one: "One" } } }, "inform", "Tell. (1 point)"],
            [{ start: "other" }, "start", "other"],
            [{ action: "send" }, "action", "send"],
        ];
        for (const [step, block, text] of steps) {
            expect(blockOf(step)).toBe(block);
            expect(rowText(step)).toBe(text);
        }
        expect(Object.keys(BLOCK_LABELS).sort()).toEqual(["action", "assign", "collect", "inform", "listen", "say", "start"]);
        expect(BLOCK_LABELS.collect).toBe("Ask");
        expect(BLOCK_LABELS.inform).toBe("Tell");
    });

    it("AC-procedure-creator-11: a Next: row names its step in place", () => {
        expect(form()).toContain("Next:");
    });

    it("AC-procedure-creator-38: the Knowledge section hides when the bot does not read it, and shows entries an instruction holds even then", () => {
        const holding = {
            ...detail,
            steps: {
                ...detail.steps,
                tell: {
                    inform: {
                        instruction: "Tell it.",
                        points: { one: "One." },
                        knowledge: [{ knowledgeBase: "reports", searchQueries: ["hours"] }],
                    },
                    next: "ask",
                },
            },
        } as unknown as ProcedureDetail;

        expect(form({ selected: "tell" })).not.toContain("Knowledge");
        expect(form({ selected: "tell", showKnowledge: true })).toContain("Knowledge");
        expect(form({ showKnowledge: true })).not.toContain("Knowledge");
        const shown = form({ draft: fromDetail(holding), selected: "tell" });
        expect(shown).toContain("Knowledge");
        expect(shown).toContain("Remove");
        expect(shown).toContain("hours");
    });

    it("AC-procedure-creator-44: End with a mistake shows it beside its branch", () => {
        const html = form({
            check: {
                state: "done",
                mistakes: [
                    {
                        code: "step.missing",
                        stage: "Save",
                        resource: { kind: "Procedure", id: "quoteRequest" },
                        path: "steps.ask.next[0].to",
                        message: "the message",
                        fix: "the fix",
                        related: [],
                    },
                ],
            },
        });
        expect(html.indexOf("the fix")).toBeGreaterThan(html.indexOf('data-branch="If"'));
        expect(html).toContain("Refuses a save");
    });

    it("AC-procedure-creator-16: when the server cannot be reached the form says so instead of looking free of mistakes", () => {
        const message = "The server could not be reached, so the mistakes below may be out of date.";
        const html = form({ check: { state: "unreachable", message } });
        expect(html).toContain(message);
        expect(html).toContain('role="alert"');
        expect(html).not.toContain("No mistakes");
    });

    describe("AC-procedure-creator-8: the conditions of an If", () => {
        const FRESH = {
            ...detail,
            localFacts: { visitDate: { type: "text", description: "d", maxLength: 20 } },
            steps: {
                ask: { say: "A", next: [{ when: {}, to: "yes" }, { when: { "order.kind": { known: true } }, to: "maybe" }, { to: "no" }] },
                yes: { say: "Y" },
                maybe: { say: "M" },
                no: { say: "N" },
            },
        } as unknown as ProcedureDetail;
        const GLOBAL = {
            name: "order.kind",
            type: "text",
            description: "d",
            writableBy: "set",
            values: null,
            min: null,
            max: null,
            maxLength: 9,
            pattern: null,
            revision: 1,
            usedBy: [],
        };

        const closed = (openConditions: string[] = []) =>
            form({ draft: fromDetail(FRESH), allFacts: [GLOBAL], selected: null, openConditions });

        it("a row with no condition is open with no step selected and cannot fold, so a condition can always be set", () => {
            const html = closed();
            expect(html).toContain("+ Choose a fact");
            expect(html).toMatch(/<button[^>]*aria-expanded="true"[^>]*aria-label="Open If of ask"[^>]*disabled/);
        });

        it("a row with a condition starts folded and reads as its line of text, like a step's row", () => {
            const html = closed();
            expect(html).toMatch(/<button[^>]*aria-expanded="false"[^>]*aria-label="Open Else if 1 of ask"/);
            expect(html).toContain("order.kind is known");
            expect(html).not.toContain('aria-label="Condition"');
        });

        it("each row unfolds on its own, whichever step is selected, and offers a global and a local fact", () => {
            const html = closed(["ask:1"]);
            expect(html).toMatch(/<button[^>]*aria-expanded="true"[^>]*aria-label="Open Else if 1 of ask"/);
            const select = /<select[^>]*aria-label="Fact"[^>]*>(.*?)<\/select>/.exec(html)?.[1] ?? "";
            expect(select).toContain(">order.kind<");
            expect(select).toContain(">visitDate<");
        });

        it("AC-procedure-creator-48: an If and an Else if have arrows, and an Else has none", () => {
            const html = closed();
            expect(html).toContain('aria-label="Move If of ask up"');
            expect(html).toContain('aria-label="Move Else if 1 of ask up"');
            expect(html.match(/aria-label="Move [^"]*"/g)?.filter((label) => label.includes(" of "))).toEqual([
                'aria-label="Move If of ask up"',
                'aria-label="Move If of ask down"',
                'aria-label="Move Else if 1 of ask up"',
                'aria-label="Move Else if 1 of ask down"',
            ]);
        });

        it("reads each kind of condition, and all of them joined", () => {
            expect(
                conditionsText({
                    "property.area": { range: { min: 500 } },
                    "order.kind": { oneOf: ["house", "flat"] },
                    city: { known: false },
                }),
            ).toBe("property.area is at least 500 and order.kind is one of house, flat and city is not known yet");
        });

        it("the If row alone has the ✕ named Remove this If, and an Else if has its own ✕", () => {
            const html = closed();
            expect(html).toMatch(/aria-label="Remove this If"[^>]*>✕</);
            expect(html).toMatch(/aria-label="Remove this Else if"[^>]*>✕</);
            expect(html.split("Remove this If")).toHaveLength(2);
            expect(html.indexOf("Remove this If")).toBeLessThan(html.indexOf('data-branch="Else if"'));
            expect(html.split("Remove this Else if")).toHaveLength(2);
            expect(html).toContain("+ Else if");
        });
    });

    describe("AC-procedure-creator-27: what a list's Add step offers", () => {
        const THEN_MORE = {
            ...detail,
            firstStep: "ask",
            steps: {
                ask: { say: "A", next: [{ when: { "property.type": { oneOf: ["land"] } }, to: "yes" }, { to: "no" }] },
                yes: { say: "Y", next: "after" },
                no: { say: "N", next: "after" },
                after: { say: "Z" },
            },
        } as unknown as ProcedureDetail;
        const STRAIGHT = {
            ...detail,
            steps: { ask: { say: "A", next: "tell" }, tell: { say: "T" } },
        } as unknown as ProcedureDetail;
        const EMPTY = { ...detail, firstStep: null, steps: {} } as unknown as ProcedureDetail;

        const menu = (of: ProcedureDetail, label: string) => {
            const html = form({ draft: fromDetail(of), offered: ["say"] });
            const select = new RegExp(`<select[^>]*aria-label="${label}"[^>]*>(.*?)</select>`).exec(html)?.[1] ?? "";
            return [...select.matchAll(/<option(?![^>]*hidden)[^>]*>([^<]*)<\/option>/g)].map((option) => option[1]);
        };

        it("an If below a step, and Next: at the end of the main list, but never End there", () => {
            expect(menu(STRAIGHT, "Add step")).toEqual(["Say", "If", "Next:"]);
        });

        it("no If with no step above it, and no Next: with no step to name", () => {
            expect(menu(EMPTY, "Add step")).toEqual(["Say"]);
        });

        it("no second If on a step that has one", () => {
            expect(menu(detail, "Add step")).toEqual(["Say", "Next:"]);
        });

        it("End in a branch with a row after it, and not in one with nothing after it", () => {
            expect(menu(THEN_MORE, "Add step to If of ask")).toEqual(["Say", "If", "Next:", "End"]);
            expect(menu(detail, "Add step to If of ask")).toEqual(["Say", "If", "Next:"]);
        });

        it("neither Next: nor End in a list that already ends", () => {
            expect(menu(detail, "Add step to Else of ask")).toEqual(["Say", "If"]);
        });

        it("AC-procedure-creator-24: + Add step is the menu's label, disabled and hidden, never an option to pick", () => {
            const html = form({ draft: fromDetail(STRAIGHT), offered: ["say"] });
            const select = /<select[^>]*aria-label="Add step"[^>]*>(.*?)<\/select>/.exec(html)?.[1] ?? "";
            expect(select).toMatch(/^<option value="" disabled="" hidden=""[^>]*>\+ Add step<\/option>/);
            expect(select.split("+ Add step")).toHaveLength(2);
        });

        it("AC-procedure-creator-44: the main list ends in an End with no ✕ and no arrows, and its + Add step sits above it", () => {
            const html = form({ draft: fromDetail(STRAIGHT) });
            const end = /<div[^>]*data-row="end"[^>]*>(.*?)<\/div>/.exec(html)?.[1] ?? "";
            expect(end).toContain("End");
            expect(end).not.toMatch(/✕|↑|↓|aria-label/);
            expect(html.indexOf('aria-label="Add step"')).toBeLessThan(html.indexOf('data-row="end"'));
            const loop = {
                ...detail,
                steps: { ask: { say: "A", next: "tell" }, tell: { say: "T", next: "ask" } },
            } as unknown as ProcedureDetail;
            const endsInNext = form({ draft: fromDetail(loop) });
            expect(endsInNext.slice(endsInNext.indexOf('aria-label="Add step"'))).not.toContain('data-row="end"');
        });

        it("AC-procedure-creator-44: in a branch ending in End, its + Add step sits above the End and nothing below it", () => {
            const ending = {
                ...detail,
                steps: {
                    ask: {
                        say: "A",
                        next: [
                            { when: { "property.type": { oneOf: ["land"] } }, to: "sorry" },
                            { when: { "property.type": { oneOf: ["flat"] } }, to: "x" },
                            { to: "after" },
                        ],
                    },
                    sorry: { say: "S" },
                    x: { say: "X", next: "after" },
                    after: { say: "Z" },
                },
            } as unknown as ProcedureDetail;
            const html = form({ draft: fromDetail(ending), offered: ["say"] });
            const branch = html.slice(html.indexOf('data-branch="If"'), html.indexOf('data-branch="Else"'));
            expect(branch).toContain('data-row="end"');
            expect(branch.indexOf('aria-label="Add step to If of ask"')).toBeLessThan(branch.indexOf('data-row="end"'));
        });

        it("the menu is the only way to add an If", () => {
            expect(form({ draft: fromDetail(STRAIGHT) })).not.toContain("Add an If below");
        });
    });
});
