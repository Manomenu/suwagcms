import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { fromDetail, type BlockName } from "./draft";
import type { ProcedureDetail } from "./api";
import { ProcedureForm } from "./ProcedureForm";
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
            whenStalled: { instruction: "Say so.", knowledge: [{ knowledgeBase: "reports", searchQueries: ["hours"] }] },
        } as unknown as ProcedureDetail;

        expect(form()).not.toContain("Knowledge");
        expect(form({ showKnowledge: true })).toContain("Knowledge");
        const shown = form({ draft: fromDetail(holding) });
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
                        message: "A rule of step 'ask' has no 'to'.",
                        fix: "Add a step to this branch, or a Next: row.",
                        related: [],
                    },
                ],
            },
        });
        expect(html.indexOf("Add a step to this branch")).toBeGreaterThan(html.indexOf('data-branch="If"'));
        expect(html).toContain("Refuses a save");
    });
});
