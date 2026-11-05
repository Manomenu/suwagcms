import { describe, expect, it } from "vitest";

import type { Fact, ProcedureDetail } from "./api";
import {
    FAILURE,
    factsThatFit,
    addRule,
    addPoint,
    addStep,
    canRemoveRule,
    collectable,
    conditionKinds,
    connect,
    deleteStep,
    disconnect,
    edgesOf,
    factsSeen,
    fromDetail,
    markFirst,
    removeLocalFact,
    removePoint,
    removeRule,
    renamePoint,
    renameStep,
    setBlock,
    startable,
    setLocalFact,
    stepOrder,
    toSave,
    valuesOffered,
    withStallText,
    withStored,
    type Draft,
} from "./draft";

// What the server sends for the spec's example: steps in the order its database keeps them, absent fields as null.
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
    return { description: "d", firstStep: null, whenStalled: null, steps: {}, localFacts: {} };
}

describe("the draft", () => {
    it("AC-procedure-creator-2: keeps every step, block and rule as stored and sends them back unchanged", () => {
        const draft = fromDetail(detail);

        expect(Object.keys(draft.steps).sort()).toEqual(["ask", "confirm", "goodbye", "mark"]);
        expect(draft.steps["ask"]?.collect).toEqual(["customer.city", "order.kind"]);
        expect(draft.steps["ask"]?.next).toEqual([{ when: { "order.kind": { oneOf: ["house"] } }, to: "mark" }, { to: "goodbye" }]);
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

    it("AC-procedure-creator-2: orders the steps by the graph, not by the order the server sent them in", () => {
        expect(stepOrder(fromDetail(detail))).toEqual(["ask", "mark", "goodbye", "confirm"]);
    });

    it("AC-procedure-creator-1: a step gets a unique name for its block, and the edits give the JSON a save sends", () => {
        let draft = empty();
        const listen = addStep(draft, "listen");
        draft = markFirst(listen.draft, listen.name);
        const first = addStep(draft, "say");
        const second = addStep(first.draft, "say");
        draft = setBlock(second.draft, second.name, { say: "Bye" });
        draft = connect(draft, "listen", "next", "say");
        draft = connect(draft, "say", "next", "say2");

        expect([listen.name, first.name, second.name]).toEqual(["listen", "say", "say2"]);
        expect(toSave(draft)).toEqual({
            description: "d",
            firstStep: "listen",
            whenStalled: null,
            steps: { listen: { listen: {}, next: "say" }, say: { say: "", next: "say2" }, say2: { say: "Bye" } },
        });
    });

    it("AC-procedure-creator-1: a rule added to a step keeps its old target as the default, and a drag from a rule's handle sets that rule", () => {
        let draft = addStep(empty(), "collect").draft;
        draft = addStep(draft, "say").draft;
        draft = connect(draft, "collect", "next", "say");
        draft = addRule(draft, "collect");
        draft = connect(draft, "collect", "rule-0", "collect");

        expect(draft.steps["collect"]?.next).toEqual([{ when: {}, to: "collect" }, { to: "say" }]);
        expect(edgesOf(draft).map((edge) => `${edge.id}->${edge.target}`)).toEqual(["collect:rule-0->collect", "collect:rule-1->say"]);
    });

    it("AC-procedure-creator-21: a start step keeps its target, and conversation and the procedure itself are not offered", () => {
        let draft = addStep(empty(), "start").draft;
        expect(toSave(draft).steps).toEqual({ start: { start: "" } });
        draft = setBlock(draft, "start", { start: "documentsList" });
        const saved = toSave(draft);

        expect(saved.steps).toEqual({ start: { start: "documentsList" } });
        expect(startable(["conversation", "documentsList", "orderValuation"], "orderValuation")).toEqual(["documentsList"]);
    });

    it("AC-procedure-creator-22: an inform keeps its instruction and its points in order, an untouched one goes back as stored, and a renamed point keeps its place", () => {
        let draft = addStep(empty(), "inform").draft;
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

    it("AC-procedure-creator-20: a context on whenStalled and on an inform goes back as stored, and emptying the text keeps a context", () => {
        const context = { articles: ["officeContact"], searchQueries: ["how long", " "] };
        const draft = fromDetail({
            ...detail,
            whenStalled: { instruction: "Say it.", context },
            steps: { tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." }, context } } },
        });

        expect(toSave(draft).whenStalled).toEqual({ instruction: "Say it.", context });
        expect(toSave(draft).steps).toEqual({ tell: { inform: { instruction: "Tell it.", points: { office: "Where it is." }, context } } });
        expect(withStallText(draft.whenStalled, "")).toEqual({ instruction: "", context });
        expect(withStallText({ instruction: "Say it." }, "")).toBeNull();
        expect(withStallText(null, "Say it.")).toEqual({ instruction: "Say it." });
    });

    it("AC-procedure-creator-8: range is offered only for an int fact, and oneOf offers an enum's values", () => {
        expect(conditionKinds(fact("order.floors", "int"))).toEqual(["oneOf", "range", "known"]);
        expect(conditionKinds(fact("order.kind", "enum"))).toEqual(["oneOf", "known"]);
        expect(conditionKinds(fact("customer.city", "text"))).toEqual(["oneOf", "known"]);
        expect(valuesOffered(fact("order.kind", "enum", "set", ["house", "flat"]))).toEqual(["house", "flat"]);
        expect(valuesOffered(fact("customer.city", "text"))).toBeNull();
    });

    it("AC-procedure-creator-8: the default rule is always last and cannot be removed while other rules remain", () => {
        let draft = addStep(empty(), "collect").draft;
        draft = addRule(addRule(draft, "collect"), "collect");
        const step = draft.steps["collect"] ?? {};
        const rules = Array.isArray(step.next) ? step.next : [];

        expect(rules.map((rule) => rule.when === undefined)).toEqual([false, false, true]);
        expect([0, 1, 2].map((index) => canRemoveRule(step, index))).toEqual([true, true, false]);
        expect(removeRule(draft, "collect", 2)).toEqual(draft);
        const fewer = removeRule(removeRule(draft, "collect", 0), "collect", 0);
        expect(fewer.steps["collect"]?.next).toBeUndefined();
    });

    it("AC-procedure-creator-8: removing the last other rule leaves a fixed next with the default's target", () => {
        let draft = addStep(addStep(empty(), "collect").draft, "say").draft;
        draft = addRule(connect(draft, "collect", "next", "say"), "collect");

        expect(removeRule(draft, "collect", 0).steps["collect"]?.next).toBe("say");
    });

    it("AC-procedure-creator-9: a collect offers no fact that only a procedure assigns", () => {
        const offered = collectable([
            fact("customer.city", "text"),
            fact("system.flag", "text", "assign"),
            fact("order.kind", "enum", "correct"),
        ]);

        expect(offered.map((one) => one.name)).toEqual(["customer.city", "order.kind"]);
    });

    it("AC-procedure-creator-10: deleting a step removes the edges and rule targets that point at it, from the saved JSON too", () => {
        const draft = deleteStep(fromDetail(detail), "goodbye");
        const saved = JSON.stringify(toSave(draft));

        expect(saved).not.toContain("goodbye");
        expect(edgesOf(draft).some((edge) => edge.target === "goodbye")).toBe(false);
        expect(deleteStep(fromDetail(detail), "ask").firstStep).toBeNull();
        expect(JSON.stringify(toSave(deleteStep(fromDetail(detail), "confirm"))).includes('"confirm"')).toBe(false);
    });

    it("AC-procedure-creator-11: an edge back to an earlier step can be drawn and is in what a save sends", () => {
        let draft = fromDetail(detail);
        draft = disconnect(draft, "confirm", "next");
        draft = connect(draft, "confirm", "next", "ask");

        expect(draft.steps["confirm"]?.next).toBe("ask");
        expect(toSave(draft).steps?.["confirm"]).toEqual({ say: "Thank you.", next: "ask" });
        expect(edgesOf(draft).some((edge) => edge.source === "confirm" && edge.target === "ask")).toBe(true);
    });

    it("AC-procedure-creator-5: removing the only edge into a step leaves no edge to it in the draft", () => {
        const draft = disconnect(fromDetail(detail), "ask", "rule-1");

        expect(edgesOf(draft).filter((edge) => edge.target === "goodbye")).toEqual([]);
        expect(draft.steps["ask"]?.next).toEqual([{ when: { "order.kind": { oneOf: ["house"] } }, to: "mark" }, { to: "" }]);
    });

    it("AC-procedure-creator-1: renaming a step renames every place that points at it, and a taken name changes nothing", () => {
        const draft = fromDetail(detail);
        const renamed = renameStep(draft, "ask", "question");

        expect(renamed.firstStep).toBe("question");
        expect(renamed.steps["confirm"]?.next).toBe("question");
        expect(Object.keys(renamed.steps)).toContain("question");
        expect(renameStep(draft, "ask", "mark")).toBe(draft);
    });
});

describe("local facts", () => {
    const bare: Draft = { description: "D", firstStep: null, whenStalled: null, steps: {}, localFacts: {} };

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

    it("AC-action-block-16: a failure edge is drawn to a step, reads back, and goes when that step is renamed or deleted", () => {
        const start: Draft = {
            description: "",
            firstStep: "check",
            whenStalled: null,
            localFacts: {},
            steps: { check: { action: "send" }, failed: { say: "Sorry" } },
        };

        const joined = connect(start, "check", FAILURE, "failed");
        expect(joined.steps["check"]?.onFailure).toBe("failed");
        expect(edgesOf(joined)).toContainEqual({ id: "check:failure", source: "check", handle: "failure", target: "failed" });
        expect(stepOrder(joined)).toEqual(["check", "failed"]);
        expect(toSave(joined).steps?.["check"]).toMatchObject({ action: "send", onFailure: "failed" });
        expect(renameStep(joined, "failed", "sorry").steps["check"]?.onFailure).toBe("sorry");
        expect(deleteStep(joined, "failed").steps["check"]?.onFailure).toBeUndefined();
        expect(disconnect(joined, "check", FAILURE).steps["check"]?.onFailure).toBeUndefined();
    });
});
