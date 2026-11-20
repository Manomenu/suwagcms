import { describe, expect, it } from "vitest";

import type { Fact, ProcedureDetail } from "./api";
import {
    addElseIf,
    addIf,
    addPoint,
    addStep,
    articlesOf,
    basesOffered,
    canEnd,
    canTakeIf,
    collectable,
    conditionKinds,
    connectStep,
    deleteStep,
    factsThatFit,
    factsSeen,
    fromDetail,
    moveStep,
    openFailure,
    removeBranch,
    removeIf,
    removeLocalFact,
    removePoint,
    renamePoint,
    renameStep,
    setBlock,
    setConditions,
    setListEnd,
    startable,
    setLocalFact,
    toSave,
    valuesOffered,
    whyNotDeletable,
    withBase,
    withStallText,
    withStored,
    type Draft,
} from "./draft";
import { findRow } from "./list";
import { isDirty, loaded } from "./state";

const detail = {
    id: "orderValuation",
    description: "The customer orders a valuation of a property.",
    firstStep: "ask",
    steps: {
        ask: {
            listen: null,
            collect: ["customer.city", "order.kind"],
            say: null,
            assign: null,
            next: [
                { when: { "order.kind": { oneOf: ["house"], range: null, known: null } }, to: "mark" },
                { when: null, to: "goodbye" },
            ],
        },
        mark: { listen: null, collect: null, say: null, assign: { "system.flag": "big" }, next: "confirm" },
        goodbye: { listen: null, collect: null, say: "Sorry, we only value houses.", assign: null, next: null },
        confirm: { listen: null, collect: null, say: "Thank you.", assign: null, next: "ask" },
    },
    whenStalled: { instruction: "Tell the customer you could not finish." },
    revision: 3,
    notReady: [],
} as unknown as ProcedureDetail;

const fact = (name: string, type: string, writableBy = "set", values: string[] | null = null): Fact => ({
    name,
    type,
    writableBy,
    values,
    description: "",
    min: null,
    max: null,
    maxLength: null,
    pattern: null,
    revision: 1,
    usedBy: [],
});

function empty(): Draft {
    return { description: "d", list: { rows: [], last: null }, notConnected: [], whenStalled: null, steps: {}, localFacts: {} };
}

describe("the draft", () => {
    it("AC-chatbot-contract-6: a save keeps a whenInterrupted it did not touch and writes none that nobody chose", () => {
        const chosen = fromDetail({ ...detail, whenInterrupted: "discard" });
        expect(toSave(chosen).whenInterrupted).toBe("discard");

        const unchosen = fromDetail({ ...detail, whenInterrupted: null });
        expect(toSave(unchosen)).not.toHaveProperty("whenInterrupted");
    });

    it("AC-procedure-creator-2: keeps every step, block and rule as stored and sends them back unchanged", () => {
        const draft = fromDetail(detail);

        expect(Object.keys(draft.steps).sort()).toEqual(["ask", "confirm", "goodbye", "mark"]);
        expect(draft.steps["ask"]?.collect).toEqual(["customer.city", "order.kind"]);
        expect(draft.steps["mark"]?.assign).toEqual({ "system.flag": "big" });
        expect(toSave(draft)).toEqual({
            description: "The customer orders a valuation of a property.",
            firstStep: "ask",
            whenStalled: { instruction: "Tell the customer you could not finish." },
            steps: {
                ask: {
                    collect: ["customer.city", "order.kind"],
                    next: [{ when: { "order.kind": { oneOf: ["house"] } }, to: "mark" }, { to: "goodbye" }],
                },
                mark: { assign: { "system.flag": "big" }, next: "confirm" },
                goodbye: { say: "Sorry, we only value houses." },
                confirm: { say: "Thank you.", next: "ask" },
            },
        });
    });

    it("AC-procedure-creator-27: an If added below the last step reaches the middle of the list, with its step, by the arrows", () => {
        const a = addStep(empty(), null, "say");
        const b = addStep(a.draft, null, "say");
        const c = addStep(b.draft, null, "say");

        const moved = moveStep(addIf(c.draft, c.name), c.name, "up");

        expect(moved.list.rows.map((row) => row.name)).toEqual([a.name, c.name, b.name]);
        expect(moved.list.rows[1]?.branches).toHaveLength(2);
    });

    it("AC-procedure-creator-29: opening and changing nothing leaves nothing to save", () => {
        const state = loaded(detail);
        expect(isDirty(state)).toBe(false);
        expect(isDirty({ ...state, draft: setBlock(state.draft, "goodbye", { say: "Bye" }) })).toBe(true);
    });

    it("AC-procedure-creator-28: a new row gets a unique name from its block, and the edits give the JSON a save sends", () => {
        let draft = empty();
        const listen = addStep(draft, null, "listen");
        const first = addStep(listen.draft, null, "say");
        const second = addStep(first.draft, null, "say");
        draft = setBlock(second.draft, second.name, { say: "Bye" });

        expect([listen.name, first.name, second.name]).toEqual(["listen", "say", "say2"]);
        expect(toSave(draft)).toEqual({
            description: "d",
            firstStep: "listen",
            whenStalled: null,
            steps: { listen: { listen: {}, next: "say" }, say: { say: "", next: "say2" }, say2: { say: "Bye" } },
        });
    });

    it("AC-procedure-creator-28: moving a row changes the saved graph as its place says and never a step's name", () => {
        let draft = addStep(empty(), null, "say").draft;
        draft = addStep(draft, null, "listen").draft;
        const moved = moveStep(draft, "listen", "up");

        expect(Object.keys(moved.steps)).toEqual(Object.keys(draft.steps));
        expect(toSave(moved).firstStep).toBe("listen");
        expect(toSave(moved).steps?.["listen"]?.next).toBe("say");
        expect(toSave(moved).steps?.["say"]?.next).toBeUndefined();
    });

    it("AC-procedure-creator-27: an If goes only directly below a step with none, comes with its Else, and a step with one cannot be deleted first", () => {
        const draft = addStep(empty(), null, "collect").draft;
        expect(canTakeIf(undefined)).toBe(false);
        expect(canTakeIf(findRow(draft.list, "collect"))).toBe(true);

        const withIf = addIf(draft, "collect");
        const row = findRow(withIf.list, "collect");
        expect(canTakeIf(row)).toBe(false);
        expect(row?.branches?.map((branch) => branch.when === undefined)).toEqual([false, true]);
        expect(whyNotDeletable(row)).toContain("Remove the If");
        expect(deleteStep(withIf, "collect")).toBe(withIf);
    });

    it("AC-procedure-creator-27: End is offered only in a branch with a row after it", () => {
        let draft = addIf(addStep(empty(), null, "collect").draft, "collect");
        const branch = { step: "collect", arm: 0 };
        expect(canEnd(draft, null)).toBe(false);
        expect(canEnd(draft, branch)).toBe(false);
        draft = addStep(draft, null, "say").draft;
        expect(canEnd(draft, branch)).toBe(true);
    });

    it("AC-procedure-creator-44: an End in a branch leaves its last step with no next, and adds no field to the format", () => {
        let draft = addIf(addStep(empty(), null, "collect").draft, "collect");
        const branch = { step: "collect", arm: 0 };
        draft = addStep(draft, branch, "listen").draft;
        draft = addStep(draft, null, "say").draft;
        const without = toSave(draft).steps?.["listen"];
        expect(without?.next).toBe("say");
        const ended = setListEnd(draft, branch, "end");
        expect(toSave(ended).steps?.["listen"]).toEqual({ listen: {} });
        expect(JSON.stringify(toSave(ended))).not.toContain("end");
    });

    it("AC-procedure-creator-8: an Else if goes before the Else, which stays last and cannot be removed", () => {
        let draft = addIf(addStep(empty(), null, "collect").draft, "collect");
        draft = addElseIf(draft, "collect");
        expect(findRow(draft.list, "collect")?.branches?.map((branch) => branch.when === undefined)).toEqual([false, false, true]);
        expect(removeBranch(draft, "collect", 2)).toBe(draft);
    });

    it("AC-procedure-creator-8: removing the last condition's branch leaves the Else's rows following the step", () => {
        let draft = addIf(addStep(empty(), null, "collect").draft, "collect");
        draft = addStep(draft, { step: "collect", arm: 1 }, "say").draft;
        draft = setConditions(draft, "collect", 0, { "a.b": { oneOf: ["c"] } });
        const removed = removeBranch(draft, "collect", 0);
        expect(removed.list.rows.map((row) => row.name)).toEqual(["collect", "say"]);
        expect(toSave(removed).steps?.["collect"]?.next).toBe("say");
    });

    it("AC-procedure-creator-10: removing an If takes the steps in its branches with it", () => {
        let draft = addIf(addStep(empty(), null, "collect").draft, "collect");
        draft = addStep(draft, { step: "collect", arm: 0 }, "say").draft;
        const removed = removeIf(draft, "collect");
        expect(Object.keys(removed.steps)).toEqual(["collect"]);
        expect(toSave(removed).steps?.["collect"]).toEqual({ collect: [] });
    });

    it("AC-procedure-creator-21: a start step keeps its target, and conversation and the procedure itself are not offered", () => {
        let draft = addStep(empty(), null, "start").draft;
        expect(toSave(draft).steps).toEqual({ start: { start: "" } });
        draft = setBlock(draft, "start", { start: "documentsList" });
        const saved = toSave(draft);

        expect(saved.steps).toEqual({ start: { start: "documentsList" } });
        expect(startable(["conversation", "documentsList", "orderValuation"], "orderValuation")).toEqual(["documentsList"]);
    });

    it("AC-procedure-creator-22: an inform keeps its instruction and its points in order, an untouched one goes back as stored, and a renamed point keeps its place", () => {
        let draft = addStep(empty(), null, "inform").draft;
        expect(toSave(draft).steps).toEqual({ inform: { inform: { instruction: "" } } });
        draft = setBlock(draft, "inform", { inform: { instruction: "Tell it.", points: { zebra: "Z", alpha: "A" } } });

        expect(toSave(draft).steps).toEqual({ inform: { inform: { instruction: "Tell it.", points: { zebra: "Z", alpha: "A" } } } });
        const stored = fromDetail({
            ...detail,
            steps: { tell: { inform: { instruction: "Tell it.", points: { zebra: "Z", alpha: "A" } } } },
        });
        expect(Object.keys(stored.steps["tell"]?.inform?.points ?? {})).toEqual(["zebra", "alpha"]);
        expect(toSave(stored).steps).toEqual({ tell: { inform: { instruction: "Tell it.", points: { zebra: "Z", alpha: "A" } } } });
        expect(Object.keys(renamePoint({ zebra: "Z", alpha: "A" }, "zebra", "yak"))).toEqual(["yak", "alpha"]);
        expect(renamePoint({ zebra: "Z", alpha: "A" }, "zebra", "alpha")).toEqual({ zebra: "Z", alpha: "A" });
        expect(addPoint({ point2: "x" })).toEqual({ point2: "x", point3: "" });
        expect(addPoint({ point2: "x", point3: "y" })).toEqual({ point2: "x", point3: "y", point4: "" });
        expect(removePoint({ zebra: "Z", alpha: "A" }, "zebra")).toEqual({ alpha: "A" });
        expect(withStored(["a", "b"], ["c", "a"])).toEqual(["a", "b", "c"]);
    });

    it("AC-procedure-creator-20: knowledge on an inform goes back as stored, and a whenStalled is its text alone", () => {
        const knowledge = [{ knowledgeBase: "reports", articles: ["officeContact"], searchQueries: ["how long", " "] }];
        const draft = fromDetail({
            ...detail,
            firstStep: "tell",
            whenStalled: { instruction: "Say it." },
            steps: { tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." }, knowledge } } },
        });

        expect(toSave(draft).whenStalled).toEqual({ instruction: "Say it." });
        expect(toSave(draft).steps).toEqual({
            tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." }, knowledge } },
        });
        expect(withStallText("")).toBeNull();
        expect(withStallText("Say it.")).toEqual({ instruction: "Say it." });
    });

    it("AC-procedure-creator-37: only a base the instruction does not use is offered, a base shows only its articles, and changing it clears them and keeps the queries", () => {
        const reports = { id: "reports", title: "Reports" };
        const office = { id: "office", title: "Office" };
        const knowledge = [{ knowledgeBase: "reports" }];

        expect(basesOffered([reports, office], knowledge)).toEqual([office]);
        expect(basesOffered([reports, office], knowledge, "reports")).toEqual([reports, office]);
        const articles = [
            { id: "reportContents", title: "Contents", knowledgeBase: "reports" },
            { id: "officeContact", title: "Contact", knowledgeBase: "office" },
        ];
        expect(articlesOf(articles, "reports").map((article) => article.id)).toEqual(["reportContents"]);
        expect(withBase({ knowledgeBase: "reports", articles: ["reportContents"], searchQueries: ["hours"] }, "office")).toEqual({
            knowledgeBase: "office",
            searchQueries: ["hours"],
        });
    });

    it("AC-procedure-creator-38: a save keeps knowledge it did not touch, and an emptied section leaves no field", () => {
        const knowledge = [{ knowledgeBase: "reports", searchQueries: ["hours"] }];
        const draft = fromDetail({ ...detail, firstStep: "tell", steps: { tell: { inform: { instruction: "Tell it.", knowledge } } } });
        expect(
            toSave(setBlock(draft, "tell", { inform: { instruction: "Say it.", knowledge } })).steps?.["tell"]?.inform?.knowledge,
        ).toEqual(knowledge);
    });

    it("AC-procedure-creator-8: range is offered only for an int fact, and oneOf offers an enum's values", () => {
        expect(conditionKinds(fact("order.floors", "int"))).toEqual(["oneOf", "range", "known"]);
        expect(conditionKinds(fact("order.kind", "enum"))).toEqual(["oneOf", "known"]);
        expect(conditionKinds(fact("customer.city", "text"))).toEqual(["oneOf", "known"]);
        expect(valuesOffered(fact("order.kind", "enum", "set", ["house", "flat"]))).toEqual(["house", "flat"]);
        expect(valuesOffered(fact("customer.city", "text"))).toBeNull();
    });

    it("AC-procedure-creator-9: a collect offers no fact that only a procedure assigns", () => {
        const offered = collectable([
            fact("customer.city", "text"),
            fact("system.flag", "text", "assign"),
            fact("order.kind", "enum", "correct"),
        ]);

        expect(offered.map((one) => one.name)).toEqual(["customer.city", "order.kind"]);
    });

    it("AC-procedure-creator-10: deleting a row joins the rows around it, and a Next: naming the step stays for the server to refuse", () => {
        let draft = addStep(empty(), null, "listen").draft;
        draft = addStep(draft, null, "say").draft;
        draft = addStep(draft, null, "collect").draft;
        draft = setListEnd(draft, null, { next: "say" });

        const deleted = deleteStep(draft, "say");
        const saved = toSave(deleted).steps;

        expect(saved?.["listen"]?.next).toBe("collect");
        expect(saved?.["collect"]?.next).toBe("say");
        expect(saved).not.toHaveProperty("say");
    });

    it("AC-procedure-creator-11: a Next: names any step, forward or back, in what a save sends", () => {
        let draft = addStep(empty(), null, "listen").draft;
        draft = setListEnd(draft, null, { next: "listen" });

        expect(toSave(draft).steps?.["listen"]).toEqual({ listen: {}, next: "listen" });
    });

    it("AC-procedure-creator-1: renaming a step renames every place that points at it, Next: included, and a taken name changes nothing", () => {
        const draft = fromDetail(detail);
        const renamed = renameStep(draft, "ask", "question");

        expect(toSave(renamed).firstStep).toBe("question");
        expect(toSave(renamed).steps?.["confirm"]?.next).toBe("question");
        expect(Object.keys(renamed.steps)).toContain("question");
        expect(renameStep(draft, "ask", "mark")).toBe(draft);
    });

    it("AC-procedure-creator-30: a step under Not connected keeps what it stored until it is moved into the list, which gives it the next of its place", () => {
        const draft = fromDetail({
            ...detail,
            firstStep: "a",
            steps: { a: { say: "a" }, b: { say: "b", next: "a" } },
        });
        expect(draft.notConnected).toEqual(["b"]);
        expect(toSave(draft).steps?.["b"]).toEqual({ say: "b", next: "a" });

        const moved = connectStep(draft, "b");
        expect(moved.notConnected).toEqual([]);
        expect(toSave(moved).steps?.["a"]?.next).toBe("b");
        expect(toSave(moved).steps?.["b"]).toEqual({ say: "b" });
    });
});

describe("local facts", () => {
    const bare: Draft = {
        description: "D",
        list: { rows: [], last: null },
        notConnected: [],
        whenStalled: null,
        steps: {},
        localFacts: {},
    };

    it("AC-procedure-creator-18: a local fact joins the facts its steps see, and a procedure without any sends no field", () => {
        const with1 = setLocalFact(bare, "visitDate", { type: "text", description: "When", maxLength: 10 });

        expect(factsSeen(with1, []).map((fact) => fact.name)).toEqual(["visitDate"]);
        expect(factsSeen(with1, [])[0]).toMatchObject({ type: "text", writableBy: "set", maxLength: 10 });
        expect(toSave(with1).localFacts).toEqual({ visitDate: { type: "text", description: "When", maxLength: 10 } });
        expect(toSave(removeLocalFact(with1, "visitDate"))).not.toHaveProperty("localFacts");
    });

    it("AC-procedure-creator-18: an int and an enum local fact keep their own constraints, are edited by replacing and removed by name", () => {
        const withInt = setLocalFact(bare, "rooms", { type: "int", description: "Rooms", min: 1, max: 9, writableBy: "set" });
        const withBoth = setLocalFact(withInt, "kind", { type: "enum", description: "Kind", values: ["house", "flat"], writableBy: "set" });
        const edited = setLocalFact(withBoth, "rooms", { type: "int", description: "Rooms", min: 2, max: 12, writableBy: "set" });

        expect(factsSeen(edited, []).find((fact) => fact.name === "rooms")).toMatchObject({ type: "int", min: 2, max: 12 });
        expect(factsSeen(edited, []).find((fact) => fact.name === "kind")).toMatchObject({ type: "enum", values: ["house", "flat"] });
        expect(Object.keys(toSave(removeLocalFact(edited, "rooms")).localFacts ?? {})).toEqual(["kind"]);
    });
});

describe("an action step", () => {
    const fact = (name: string, over: Partial<Fact>): Fact => ({
        name,
        type: "text",
        description: "",
        writableBy: "set",
        values: null,
        min: null,
        max: null,
        maxLength: 10,
        pattern: null,
        revision: 1,
        usedBy: [],
        ...over,
    });

    it("AC-action-block-16: a parameter row offers only the facts whose every value fits it, and a result row the facts that take all it gives", () => {
        const short = fact("short", { maxLength: 10 });
        const long = fact("long", { maxLength: 500 });
        const number = fact("number", { type: "int", min: 1, max: 5, maxLength: null });
        const field = { type: "text", maxLength: 200, values: null, min: null, max: null };

        expect(factsThatFit("with", field, [short, long, number]).map((found) => found.name)).toEqual(["short"]);
        expect(factsThatFit("results", field, [short, long, number]).map((found) => found.name)).toEqual(["long"]);
    });

    it("AC-action-block-16: On failure is a branch of the row: an empty one goes on below, its steps follow, and it reads back", () => {
        let draft: Draft = {
            description: "",
            list: { rows: [], last: null },
            notConnected: [],
            whenStalled: null,
            localFacts: {},
            steps: {},
        };
        draft = addStep(draft, null, "action").draft;
        draft = addStep(draft, null, "say").draft;
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", next: "say" });

        draft = openFailure(draft, "action");
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "say", next: "say" });

        draft = addStep(draft, { step: "action", arm: "failure" }, "listen").draft;
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "listen", next: "say" });
        expect(toSave(draft).steps?.["listen"]).toEqual({ listen: {}, next: "say" });
        expect(toSave(renameStep(draft, "listen", "wait")).steps?.["action"]?.onFailure).toBe("wait");
    });

    it("AC-action-block-16: When unavailable is an optional branch of the row: empty, the step has none; its first step or Next: names it, and it reads back", () => {
        let draft: Draft = {
            description: "",
            list: { rows: [], last: null },
            notConnected: [],
            whenStalled: null,
            localFacts: {},
            steps: {},
        };
        draft = addStep(draft, null, "action").draft;
        draft = addStep(draft, null, "say").draft;
        draft = openFailure(draft, "action");
        const ref = { step: "action", arm: "unavailable" as const };
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "say", next: "say" });
        expect(canEnd(draft, ref)).toBe(false);

        draft = addStep(draft, ref, "listen").draft;
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "say", onUnavailable: "listen", next: "say" });
        expect(toSave(draft).steps?.["listen"]).toEqual({ listen: {}, next: "say" });
        expect(toSave(renameStep(draft, "listen", "wait")).steps?.["action"]?.onUnavailable).toBe("wait");
        const reopened = fromDetail({ ...toSave(draft), revision: 1, notReady: [] } as unknown as ProcedureDetail);
        expect(toSave(reopened)).toEqual(toSave(draft));

        draft = deleteStep(draft, "listen");
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "say", next: "say" });

        draft = setListEnd(draft, ref, { next: "say" });
        expect(toSave(draft).steps?.["action"]).toEqual({ action: "", onFailure: "say", onUnavailable: "say", next: "say" });
    });
});
