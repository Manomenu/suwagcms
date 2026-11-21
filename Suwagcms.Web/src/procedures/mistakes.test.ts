import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { type Mistake } from "@/api/client";

import type { ProcedureDetail } from "./api";
import type { Draft } from "./draft";
import { checkOf, conditionOfPlace, consequenceOf, placeOf, stepOfPlace } from "./mistakes";
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
    list: { rows: [{ name: "ask" }], last: null },
    notConnected: ["goodbye"],
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

const refused = (status: number, mistakes: Mistake[] = [], message = "no") =>
    Promise.reject(
        new ApiError(status, {
            detail: message,
            code: "definition.invalid",
            mistakes: mistakes.map(({ path, ...rest }) => ({ ...rest, at: path })),
        }),
    );

describe("the live list of mistakes", () => {
    it("AC-procedure-creator-3: shows what the server's dry run reports, whichever stage it speaks of", async () => {
        const version = mistake("Version", "steps.goodbye", "No path from firstStep reaches step 'goodbye'.");
        const save = mistake("Save", "steps.ask.next", "names no step");

        expect(await checkOf(Promise.resolve(detail(1, [version])))).toEqual({ state: "done", mistakes: [version] });
        expect(await checkOf(refused(422, [save]))).toEqual({ state: "done", mistakes: [save] });
        expect(await checkOf(Promise.resolve(detail(1)))).toEqual({ state: "done", mistakes: [] });
    });

    it("AC-procedure-creator-4: says whether a mistake refuses a save or only stops a version, and clicking it names its step", () => {
        expect(consequenceOf(mistake("Save", "steps.ask"))).toBe("Refuses a save");
        expect(consequenceOf(mistake("Version", "steps.ask"))).toBe("Stops a version");
        expect(stepOfPlace("step:ask")).toBe("ask");
        expect(stepOfPlace("branch:ask:1")).toBeNull();
        expect(conditionOfPlace("branch:ask:1")).toBe("ask:1");
        expect(conditionOfPlace("step:ask")).toBeNull();
        expect(stepOfPlace("knowledge:tell:0")).toBe("tell");
        expect(stepOfPlace("field:description")).toBeNull();
    });

    it("AC-action-block-16: a mistake on When unavailable belongs beside that branch, and clicking it opens the step", () => {
        expect(placeOf(mistake("Save", "steps.ask.onUnavailable"), "p", draft)).toBe("unavailable:ask");
        expect(stepOfPlace("unavailable:ask")).toBe("ask");
    });

    it("AC-procedure-creator-3: a mistake belongs beside its row, branch or field, and one with no place on the page at the top of the form", () => {
        const at = (path: string) => placeOf(mistake("Version", path), "p", draft);

        expect(at("steps.goodbye")).toBe("step:goodbye");
        expect(at("steps.ask.collect[0]")).toBe("step:ask");
        expect(at("steps.ask.next")).toBe("step:ask");
        expect(at("steps.ask.next[1].to")).toBe("branch:ask:1");
        expect(at("steps.ask.onFailure")).toBe("failure:ask");
        expect(at("steps.ask.inform.knowledge[2].articles[0]")).toBe("knowledge:ask:2");
        expect(at("whenStalled.instruction")).toBe("field:whenStalled");
        expect(at("description")).toBe("field:description");
        expect(at("whenInterrupted")).toBe("field:whenInterrupted");
        expect(at("localFacts.visitDate")).toBe("field:localFacts");
        expect(at("firstStep")).toBe("top");
        expect(at("steps.gone")).toBe("top");
        expect(placeOf({ ...mistake("Version", "procedures"), resource: { kind: "Definition", id: "" } }, "p", draft)).toBe("top");
        expect(placeOf({ ...mistake("Version", "description"), resource: { kind: "Procedure", id: "other" } }, "p", draft)).toBe("top");
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

    it("AC-procedure-creator-6: a save with a Save mistake is refused and the page stays as it was", async () => {
        const save = mistake("Save", "steps.ask.next");
        const edited = reduce(loaded(detail(1)), { type: "edited", draft: { ...draft, description: "changed" } });

        const next = reduce(edited, { type: "saved", outcome: await outcomeOf(refused(422, [save])), sent: "" });

        expect(next.draft).toBe(edited.draft);
        expect(next.revision).toBe(1);
        expect(next.notice).toEqual({ kind: "refused", mistakes: [save] });
        expect(isDirty(next)).toBe(true);
    });

    it("AC-procedure-creator-7: a save on an old revision is refused, the page stays, and a reload replaces it with what is stored", async () => {
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
