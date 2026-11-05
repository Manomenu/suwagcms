import { describe, expect, it } from "vitest";

import { ApiError, type Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import type { Draft } from "./draft";
import { checkOf, consequenceOf, stepsWithMistakes, targetOf } from "./mistakes";
import { isDirty, loaded, outcomeOf, reduce } from "./state";

const mistake = (stage: "Save" | "Version", path: string, message = "m"): Mistake => ({
    code: `c.${path}`,
    stage,
    resource: { kind: "Procedure", id: "p" },
    path,
    message,
    fix: "Do this.",
    related: [],
});

const draft: Draft = {
    description: "d",
    firstStep: "ask",
    whenStalled: null,
    steps: { ask: { say: "hi" }, goodbye: { say: "bye" } },
    localFacts: {},
};

function detail(revision: number, notReady: Mistake[] = [], say = "hi"): ProcedureDetail {
    return {
        id: "p",
        description: "d",
        firstStep: "ask",
        steps: { ask: { say } },
        whenStalled: null,
        revision,
        notReady,
    };
}

const refused = (status: number, mistakes: Mistake[] = [], message = "no") => Promise.reject(new ApiError(message, mistakes, status));

describe("the live list of mistakes", () => {
    it("AC-procedure-creator-3: shows what the server's dry run reports, whichever stage it speaks of", async () => {
        const version = mistake("Version", "steps.goodbye", "No path from firstStep reaches step 'goodbye'.");
        const save = mistake("Save", "steps.ask.next", "names no step");

        expect(await checkOf(Promise.resolve(detail(1, [version])))).toEqual({ state: "done", mistakes: [version] });
        expect(await checkOf(refused(422, [save]))).toEqual({ state: "done", mistakes: [save] });
        expect(await checkOf(Promise.resolve(detail(1)))).toEqual({ state: "done", mistakes: [] });
    });

    it("AC-procedure-creator-4: says whether a mistake refuses a save or only stops a version, and clicking it names its step or the panel", () => {
        expect(consequenceOf(mistake("Save", "steps.ask"))).toBe("Refuses a save");
        expect(consequenceOf(mistake("Version", "steps.ask"))).toBe("Stops a version");
        expect(targetOf(mistake("Version", "steps.goodbye"), draft)).toBe("goodbye");
        expect(targetOf(mistake("Save", "steps.ask.next[1].to"), draft)).toBe("ask");
        expect(targetOf(mistake("Version", "firstStep"), draft)).toBeNull();
        expect(targetOf(mistake("Version", "steps.gone"), draft)).toBeNull();
        expect([...stepsWithMistakes([mistake("Version", "steps.goodbye"), mistake("Version", "whenStalled")], draft)]).toEqual([
            "goodbye",
        ]);
    });

    it("AC-procedure-creator-16: says it could not check when the server cannot be reached, instead of an empty list", async () => {
        const down = await checkOf(Promise.reject(new TypeError("Failed to fetch")));
        const broken = await checkOf(refused(500, [], "boom"));

        expect(down.state).toBe("unreachable");
        expect(down).toMatchObject({ message: expect.stringContaining("could not be reached") as unknown });
        expect(broken).toMatchObject({ state: "unreachable", message: "The server answered: boom" });
    });

    it("AC-procedure-creator-7: a dry run against an old revision is told apart from a mistake", async () => {
        expect(await checkOf(refused(409, [], "it is now revision 2"))).toEqual({ state: "stale", message: "it is now revision 2" });
    });
});

describe("saving", () => {
    it("AC-procedure-creator-6: a save with only Version mistakes succeeds and the list stays as the server answered", async () => {
        const version = mistake("Version", "steps.goodbye");
        const state = loaded(detail(1));

        const outcome = await outcomeOf(Promise.resolve(detail(2, [version])));
        const next = reduce(state, { type: "saved", outcome, sent: JSON.stringify({}) });

        expect(outcome.kind).toBe("saved");
        expect(next.revision).toBe(2);
        expect(next.notReady).toEqual([version]);
        expect(next.notice).toBeNull();
    });

    it("AC-procedure-creator-6: a save with a Save mistake is refused and the drawing stays as it was", async () => {
        const save = mistake("Save", "steps.ask.next");
        const edited = reduce(loaded(detail(1)), { type: "edited", draft: { ...draft, description: "changed" } });

        const next = reduce(edited, { type: "saved", outcome: await outcomeOf(refused(422, [save])), sent: "" });

        expect(next.draft).toBe(edited.draft);
        expect(next.revision).toBe(1);
        expect(next.notice).toEqual({ kind: "refused", mistakes: [save] });
        expect(isDirty(next)).toBe(true);
    });

    it("AC-procedure-creator-7: a save on an old revision is refused, the drawing stays, and a reload replaces it with what is stored", async () => {
        const edited = reduce(loaded(detail(1)), { type: "edited", draft: { ...draft, description: "mine" } });

        const stale = reduce(edited, { type: "saved", outcome: await outcomeOf(refused(409, [], "it is now revision 2")), sent: "" });
        const stillEdited = reduce(stale, { type: "edited", draft: { ...stale.draft, description: "mine, more" } });
        const reloaded = reduce(stale, { type: "loaded", detail: detail(2, [], "agent's") });

        expect(stale.draft.description).toBe("mine");
        expect(stale.notice).toEqual({ kind: "stale", message: "it is now revision 2" });
        expect(stillEdited.notice?.kind).toBe("stale");
        expect(reloaded.revision).toBe(2);
        expect(reloaded.draft.steps["ask"]?.say).toBe("agent's");
        expect(reloaded.notice).toBeNull();
        expect(isDirty(reloaded)).toBe(false);
    });

    it("AC-procedure-creator-17: a draft that differs from what was saved is unsaved, so leaving asks first, and saving clears it", async () => {
        const clean = loaded(detail(1));
        const edited = reduce(clean, { type: "edited", draft: { ...clean.draft, description: "other" } });
        const sent = JSON.stringify({ description: "other", firstStep: "ask", whenStalled: null, steps: { ask: { say: "hi" } } });

        const saved = reduce(edited, { type: "saved", outcome: await outcomeOf(Promise.resolve(detail(2))), sent });

        expect(isDirty(clean)).toBe(false);
        expect(isDirty(edited)).toBe(true);
        expect(isDirty(saved)).toBe(false);
        expect(isDirty(reduce(saved, { type: "edited", draft: clean.draft }))).toBe(true);
        expect(isDirty(reduce(edited, { type: "edited", draft: clean.draft }))).toBe(false);
    });
});
