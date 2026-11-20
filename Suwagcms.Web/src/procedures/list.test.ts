import { describe, expect, it } from "vitest";

import type { ProcedureDetail } from "./api";
import { fromDetail, toSave, type Rule, type Step } from "./draft";
import {
    canMoveIf,
    canSwapBranch,
    firstStepOf,
    fallOf,
    findRow,
    linksOf,
    listOf,
    moveIf,
    moveRow,
    namesIn,
    swapBranch,
    type StepList,
    type StepRow,
} from "./list";

type Graph = Record<string, Step>;

const when = (fact: string, value: string) => ({ [fact]: { oneOf: [value] } });

function lines(list: StepList, indent = ""): string[] {
    const out: string[] = [];
    list.rows.forEach((row, index) => {
        out.push(`${indent}${index + 1}. ${row.name}`);
        if (row.failure) out.push(`${indent}On failure`, ...lines(row.failure, `${indent}  `));
        if (row.unavailable) out.push(`${indent}When unavailable`, ...lines(row.unavailable, `${indent}  `));
        if (row.success) out.push(`${indent}On success`, ...lines(row.success, `${indent}  `));
        (row.branches ?? []).forEach((branch, at) => {
            out.push(indent + (branch.when === undefined ? "Else" : at === 0 ? "If" : "Else if"));
            out.push(...lines(branch.list, `${indent}  `));
        });
    });
    if (list.last === "end") out.push(`${indent}End`);
    else if (list.last !== null) out.push(`${indent}Next: ${list.last.next}`);
    return out;
}

const read = (first: string | null, steps: Graph) => lines(listOf(first, steps).list);

const QUOTE_REQUEST: Graph = {
    askProperty: {
        collect: ["property.type"],
        next: [{ when: when("property.type", "land"), to: "askContact" }, { to: "askAge" }],
    },
    askAge: {
        collect: ["property.age"],
        next: [{ when: { "property.age": { range: { min: 50 } } }, to: "askRenovation" }, { to: "askContact" }],
    },
    askRenovation: { collect: ["property.lastRenovationYear"], next: "tellConditionReview" },
    tellConditionReview: { inform: { instruction: "Tell." }, next: "askContact" },
    askContact: { collect: ["customer.email"], next: "whatHappensNext" },
    whatHappensNext: { inform: { instruction: "Tell." } },
};

const VALUATION_REPORT: Graph = {
    askDetails: { collect: ["customer.firstName"], next: "askDelivery" },
    askDelivery: {
        collect: ["reportDelivery"],
        next: [{ when: when("reportDelivery", "phone"), to: "askPhone" }, { to: "askEmail" }],
    },
    askEmail: { collect: ["customer.email"], next: "tellEmailDelivery" },
    tellEmailDelivery: { inform: { instruction: "Tell." } },
    askPhone: { collect: ["customer.phone"], next: "tellPhoneDelivery" },
    tellPhoneDelivery: { inform: { instruction: "Tell." } },
};

describe("the canonical list of a stored graph", () => {
    it("AC-procedure-creator-2: conversation, as shipped, reads as a listen and a Next: to itself", () => {
        expect(read("wait", { wait: { listen: true, next: "wait" } })).toEqual(["1. wait", "Next: wait"]);
    });

    it("AC-procedure-creator-2: inspectionVisit, as shipped, reads as two rows", () => {
        expect(
            read("askDetails", {
                askDetails: { collect: ["customer.firstName"], next: "whatHappensNext" },
                whatHappensNext: { inform: { instruction: "Tell." } },
            }),
        ).toEqual(["1. askDetails", "2. whatHappensNext"]);
    });

    it("AC-procedure-creator-2: quoteRequest, as shipped, nests the age and goes on to the contact after both Ifs", () => {
        expect(read("askProperty", QUOTE_REQUEST)).toEqual([
            "1. askProperty",
            "If",
            "Else",
            "  1. askAge",
            "  If",
            "    1. askRenovation",
            "    2. tellConditionReview",
            "  Else",
            "2. askContact",
            "3. whatHappensNext",
        ]);
    });

    it("AC-procedure-creator-2: valuationReport, as shipped, has two branches that end, and no End to say so", () => {
        expect(read("askDetails", VALUATION_REPORT)).toEqual([
            "1. askDetails",
            "2. askDelivery",
            "If",
            "  1. askPhone",
            "  2. tellPhoneDelivery",
            "Else",
            "  1. askEmail",
            "  2. tellEmailDelivery",
        ]);
    });

    it("AC-procedure-creator-2: the order of the steps in a file never matters", () => {
        const reversed = Object.fromEntries(Object.entries(QUOTE_REQUEST).reverse());
        expect(read("askProperty", reversed)).toEqual(read("askProperty", QUOTE_REQUEST));
    });

    it("AC-procedure-creator-11: a step whose next is not the following row shows that row, forward and back", () => {
        expect(
            read("ask", {
                ask: { collect: ["a"], next: [{ when: when("a", "1"), to: "again" }, { to: "done" }] },
                again: { say: "Again", next: "ask" },
                done: { say: "Done" },
            }),
        ).toEqual(["1. ask", "If", "  1. again", "  Next: ask", "Else", "  1. done"]);
        expect(
            read("s", {
                s: { say: "s", next: [{ when: when("a", "1"), to: "p" }, { to: "q" }] },
                p: { say: "p", next: [{ when: when("b", "1"), to: "t" }, { to: "r" }] },
                q: { say: "q", next: "r" },
                r: { say: "r", next: "t" },
                t: { say: "t" },
            }),
        ).toEqual(["1. s", "If", "  1. p", "  If", "  Else", "    Next: r", "  2. t", "  End", "Else", "  1. q", "2. r", "Next: t"]);
    });

    it("AC-procedure-creator-44: a branch that ends while the others come together shows End, and the main list stores none: the editor draws its End", () => {
        expect(
            read("s", {
                s: { say: "s", next: [{ when: when("a", "1"), to: "sorry" }, { when: when("b", "1"), to: "x" }, { to: "j" }] },
                sorry: { say: "no" },
                x: { say: "x", next: "j" },
                j: { say: "j" },
            }),
        ).toEqual(["1. s", "If", "  1. sorry", "  End", "Else if", "  1. x", "Else", "2. j"]);
    });

    it("AC-procedure-creator-29: two rules to one step are two branches going on to it, the format's only OR across facts", () => {
        expect(
            read("ask", {
                ask: {
                    collect: ["a"],
                    next: [{ when: when("a", "1"), to: "mark" }, { when: when("b", "2"), to: "mark" }, { to: "goodbye" }],
                },
                mark: { say: "m" },
                goodbye: { say: "bye" },
            }),
        ).toEqual(["1. ask", "If", "Else if", "Else", "  1. goodbye", "  End", "2. mark"]);
    });

    it("AC-procedure-creator-30: what no path reaches, and every step with no first step, is under Not connected", () => {
        const steps: Graph = { a: { say: "a" }, b: { say: "b", next: "a" } };
        expect(listOf("a", steps).notConnected).toEqual(["b"]);
        expect(listOf(null, steps)).toEqual({ list: { rows: [], last: null }, notConnected: ["a", "b"] });
    });

    it("AC-procedure-creator-44: an action whose failure ends has both its branches, and one that rejoins goes on below", () => {
        expect(read("s", { s: { action: "f", onFailure: "sorry", next: "go" }, sorry: { say: "no" }, go: { say: "go" } })).toEqual([
            "1. s",
            "On failure",
            "  1. sorry",
            "On success",
            "  1. go",
        ]);
        expect(
            read("s", { s: { action: "f", onFailure: "sorry", next: "go" }, sorry: { say: "no", next: "go" }, go: { say: "go" } }),
        ).toEqual(["1. s", "On failure", "  1. sorry", "On success", "2. go"]);
    });
});

describe("saving a list", () => {
    const row = (name: string) => ({ name });

    it("AC-procedure-creator-26: the first row is the first step, and a row's next is the row below", () => {
        const list: StepList = { rows: [row("a"), row("b")], last: null };
        expect(firstStepOf(list)).toBe("a");
        expect(firstStepOf({ rows: [], last: null })).toBeNull();
        expect(linksOf(list)).toEqual({ a: { next: "b" }, b: {} });
    });

    it("AC-procedure-creator-26: a rule per branch in order, a branch's last row goes to the row after the If, and an empty branch's to is that row", () => {
        const condition = when("a", "1");
        const list: StepList = {
            rows: [
                { name: "a", branches: [{ when: condition, list: { rows: [row("x")], last: null } }, { list: { rows: [], last: null } }] },
                row("b"),
            ],
            last: null,
        };
        expect(linksOf(list)).toEqual({ a: { next: [{ when: condition, to: "x" }, { to: "b" }] }, x: { next: "b" }, b: {} });
    });

    it("AC-procedure-creator-26: the row after an If is found looking outward through the lists around it", () => {
        const list: StepList = {
            rows: [
                {
                    name: "a",
                    branches: [
                        {
                            when: {},
                            list: {
                                rows: [
                                    {
                                        name: "b",
                                        branches: [
                                            { when: {}, list: { rows: [row("c")], last: null } },
                                            { list: { rows: [], last: null } },
                                        ],
                                    },
                                ],
                                last: null,
                            },
                        },
                        { list: { rows: [], last: null } },
                    ],
                },
                row("z"),
            ],
            last: null,
        };
        expect(linksOf(list)["c"]).toEqual({ next: "z" });
        expect(fallOf(list, { step: "b", arm: 0 })).toBe("z");
        expect(fallOf(list, null)).toBeNull();
    });

    it("AC-procedure-creator-11: a Next: row names any step, as a next or as a rule's to", () => {
        const list: StepList = {
            rows: [
                {
                    name: "a",
                    branches: [{ when: {}, list: { rows: [], last: { next: "a" } } }, { list: { rows: [row("b")], last: { next: "a" } } }],
                },
            ],
            last: null,
        };
        expect(linksOf(list)).toEqual({ a: { next: [{ when: {}, to: "a" }, { to: "b" }] }, b: { next: "a" } });
    });

    it("AC-procedure-creator-44: End leaves the step above with no next, and a branch with no step before End has a blank target for the server to refuse", () => {
        const list: StepList = {
            rows: [
                {
                    name: "a",
                    branches: [
                        { when: {}, list: { rows: [row("sorry")], last: "end" } },
                        { when: {}, list: { rows: [], last: "end" } },
                        { list: { rows: [], last: null } },
                    ],
                },
                row("z"),
            ],
            last: null,
        };
        expect(linksOf(list)).toEqual({ a: { next: [{ when: {}, to: "sorry" }, { when: {}, to: "" }, { to: "z" }] }, sorry: {}, z: {} });
    });

    it("AC-procedure-creator-28: moving a row in reading order goes into and out of branches, with what hangs on it", () => {
        const start: StepList = {
            rows: [
                row("a"),
                { name: "b", branches: [{ when: {}, list: { rows: [row("x")], last: null } }, { list: { rows: [], last: null } }] },
                row("c"),
            ],
            last: null,
        };
        expect(lines(moveRow(start, "a", "down"))).toEqual(["1. b", "If", "  1. a", "  2. x", "Else", "2. c"]);
        expect(lines(moveRow(start, "x", "down"))).toEqual(["1. a", "2. b", "If", "Else", "  1. x", "3. c"]);
        expect(lines(moveRow(start, "x", "up"))).toEqual(["1. a", "2. x", "3. b", "If", "Else", "4. c"]);
        expect(lines(moveRow(start, "c", "up"))).toEqual(["1. a", "2. b", "If", "  1. x", "Else", "  1. c"]);
        expect(lines(moveRow(start, "b", "up"))).toEqual(["1. b", "If", "  1. x", "Else", "2. a", "3. c"]);
        expect(moveRow(start, "a", "up")).toEqual(start);
        expect(moveRow(start, "c", "down")).toEqual(start);
    });
});

describe("moving an If", () => {
    const blockOn = (name: string): StepRow => ({
        name,
        branches: [
            { when: when("f", "1"), list: { rows: [{ name: "x" }], last: null } },
            { when: when("g", "1"), list: { rows: [{ name: "y" }], last: null } },
            { list: { rows: [{ name: "z" }], last: null } },
        ],
    });
    const LIST: StepList = { rows: [{ name: "a" }, blockOn("b"), { name: "c" }], last: null };

    it("AC-procedure-creator-48: the block joins the neighbouring step, its steps stay in it, and the list keeps its order", () => {
        expect(lines(moveIf(LIST, "b", "up"))).toEqual(["1. a", "If", "  1. x", "Else if", "  1. y", "Else", "  1. z", "2. b", "3. c"]);
        expect(lines(moveIf(LIST, "b", "down"))).toEqual(["1. a", "2. b", "3. c", "If", "  1. x", "Else if", "  1. y", "Else", "  1. z"]);
    });

    it("AC-procedure-creator-48: moving back restores the list", () => {
        expect(moveIf(moveIf(LIST, "b", "up"), "a", "down")).toEqual(LIST);
    });

    it("AC-procedure-creator-48: the rules leave one step and join the other, and what falls through follows the new place", () => {
        const links = linksOf(moveIf(LIST, "b", "up"));
        expect(links["a"]?.next).toEqual([{ when: when("f", "1"), to: "x" }, { when: when("g", "1"), to: "y" }, { to: "z" }]);
        expect(links["b"]).toEqual({ next: "c" });
        expect(links["x"]).toEqual({ next: "b" });
    });

    it("AC-procedure-creator-48: it cannot go with no step on that side or onto a step with an If of its own, or one holding what follows it", () => {
        expect(canMoveIf(LIST, "b", "up")).toBe(true);
        const first: StepList = { rows: [blockOn("b"), { name: "c" }], last: null };
        expect(canMoveIf(first, "b", "up")).toBe(false);
        expect(moveIf(first, "b", "up")).toEqual(first);
        const last: StepList = { rows: [{ name: "a" }, blockOn("b")], last: null };
        expect(canMoveIf(last, "b", "down")).toBe(false);
        const twoIfs: StepList = { rows: [blockOn("a"), blockOn("b")], last: null };
        expect(canMoveIf(twoIfs, "b", "up")).toBe(false);
        const holding: StepList = {
            rows: [{ name: "a", failure: { rows: [], last: null }, success: { rows: [{ name: "s" }], last: null } }, blockOn("b")],
            last: null,
        };
        expect(canMoveIf(holding, "b", "up")).toBe(false);
    });

    it("AC-procedure-creator-48: an Else if swaps with the If, each keeping its steps, and the Else stays last", () => {
        const swapped = swapBranch(LIST, "b", 1, "up");
        expect(lines(swapped)).toEqual(["1. a", "2. b", "If", "  1. y", "Else if", "  1. x", "Else", "  1. z", "3. c"]);
        expect(findRow(swapped, "b")?.branches?.[0]?.when).toEqual(when("g", "1"));
        const row = blockOn("b");
        expect(canSwapBranch(row, 1, "up")).toBe(true);
        expect(canSwapBranch(row, 1, "down")).toBe(false);
        expect(canSwapBranch(row, 2, "up")).toBe(false);
        expect(swapBranch(LIST, "b", 1, "down")).toEqual(LIST);
    });

    it("AC-procedure-creator-48: for any graph, a move keeps every step, and a save and a reopen build the same graph", () => {
        let moves = 0;
        for (let seed = 1; seed <= 2000; seed++) {
            const { first, steps } = randomGraph(seed);
            const detail = { description: "D", firstStep: first, steps, revision: 1, notReady: [] } as unknown as ProcedureDetail;
            const draft = fromDetail(detail);
            const blocks = namesIn(draft.list).filter((name) => findRow(draft.list, name)?.branches !== undefined);
            for (const name of blocks) {
                for (const direction of ["up", "down"] as const) {
                    if (!canMoveIf(draft.list, name, direction)) continue;
                    moves++;
                    const list = moveIf(draft.list, name, direction);
                    const saved = toSave({ ...draft, list });
                    expect(Object.keys(saved.steps ?? {}).sort(), `seed ${seed}`).toEqual(Object.keys(steps).sort());
                    const again = toSave(
                        fromDetail({ ...detail, firstStep: saved.firstStep, steps: saved.steps } as unknown as ProcedureDetail),
                    );
                    expect(again.steps, `seed ${seed}, ${name} ${direction}, reopened`).toEqual(saved.steps);
                }
            }
        }
        expect(moves).toBeGreaterThan(100);
    });
});

function generator(seed: number) {
    let state = seed;
    return (limit: number) => {
        state = (state * 1664525 + 1013904223) % 4294967296;
        return Math.floor((state / 4294967296) * limit);
    };
}

function randomGraph(seed: number): { first: string | null; steps: Graph } {
    const pick = generator(seed);
    const names = Array.from({ length: 1 + pick(9) }, (_, index) => `s${index}`);
    const any = () => names[pick(names.length)] ?? "s0";
    const rulesOf = (): Rule[] => {
        const rules: Rule[] = Array.from({ length: 1 + pick(3) }, (_, index) => ({ when: when(`f${index}`, "1"), to: any() }));
        const shape = pick(4);
        if (shape === 0) return rules;
        if (shape === 1) return [{ to: any() }];
        const at = shape === 2 ? rules.length : pick(rules.length);
        return [...rules.slice(0, at), { to: any() }, ...rules.slice(at)];
    };
    const steps: Graph = {};
    for (const name of names) {
        const kind = pick(10);
        if (kind < 2) steps[name] = { say: name };
        else if (kind < 5) steps[name] = { say: name, next: any() };
        else if (kind < 8) steps[name] = { say: name, next: rulesOf() };
        else {
            const shape = pick(3);
            const unavailable = pick(3);
            steps[name] = {
                action: "x",
                onFailure: any(),
                ...(unavailable === 0 ? {} : { onUnavailable: any() }),
                ...(shape === 0 ? {} : { next: shape === 1 ? any() : rulesOf() }),
            };
        }
    }
    return { first: pick(10) === 0 ? null : (names[0] ?? "s0"), steps };
}

function nextAsSaved(next: Step["next"]): Step["next"] {
    return Array.isArray(next) && next.length === 1 && next[0]?.when === undefined ? next[0]?.to : next;
}

describe("the round trip", () => {
    it("AC-procedure-creator-29: for any graph, jumps, loops and several rules to one step included, the list builds back the same graph", () => {
        for (let seed = 1; seed <= 2000; seed++) {
            const { first, steps } = randomGraph(seed);
            const { list, notConnected } = listOf(first, steps);
            const links = linksOf(list);
            expect(firstStepOf(list), `seed ${seed}`).toBe(first);
            for (const [name, step] of Object.entries(steps)) {
                if (notConnected.includes(name)) continue;
                expect(links[name]?.next, `seed ${seed}, next of ${name}`).toEqual(nextAsSaved(step.next));
                expect(links[name]?.onFailure, `seed ${seed}, onFailure of ${name}`).toEqual(step.onFailure);
                expect(links[name]?.onUnavailable, `seed ${seed}, onUnavailable of ${name}`).toEqual(step.onUnavailable);
            }
        }
    });

    it("AC-procedure-creator-29: what a save would send holds every connected step as the graph says and a step Not connected as stored, and a second round changes nothing", () => {
        for (let seed = 1; seed <= 2000; seed++) {
            const { first, steps } = randomGraph(seed);
            const detail = { description: "D", firstStep: first, steps, revision: 1, notReady: [] } as unknown as ProcedureDetail;
            const saved = toSave(fromDetail(detail));
            const { notConnected } = listOf(first, steps);
            const expected = Object.fromEntries(
                Object.entries(steps).map(([name, step]) => {
                    if (notConnected.includes(name)) return [name, step];
                    const { next, ...rest } = step;
                    const written = next === undefined ? undefined : nextAsSaved(next);
                    return [name, written === undefined ? rest : { ...rest, next: written }];
                }),
            );
            expect(saved.firstStep, `seed ${seed}`).toBe(first);
            expect(saved.steps, `seed ${seed}`).toEqual(expected);
            const again = toSave(fromDetail({ ...detail, steps: saved.steps } as unknown as ProcedureDetail));
            expect(again, `seed ${seed}, second round`).toEqual(saved);
        }
    });

    it("AC-procedure-creator-29: When unavailable is a branch beside On failure, and one naming the step that follows or the failure step keeps it", () => {
        const steps: Graph = {
            s: { action: "f", onFailure: "sorry", onUnavailable: "call", next: "go" },
            sorry: { say: "no" },
            call: { say: "we call", next: "go" },
            go: { say: "go" },
        };
        expect(read("s", steps)).toEqual([
            "1. s",
            "On failure",
            "  1. sorry",
            "  End",
            "When unavailable",
            "  1. call",
            "On success",
            "2. go",
        ]);
        for (const onUnavailable of ["go", "sorry"]) {
            const graph: Graph = { ...steps, s: { action: "f", onFailure: "sorry", onUnavailable, next: "go" } };
            expect(linksOf(listOf("s", graph).list)["s"], onUnavailable).toEqual({ onFailure: "sorry", onUnavailable, next: "go" });
        }
    });

    it("AC-procedure-creator-29: a rule list holding only a default is written as a fixed next, which means the same", () => {
        const { list } = listOf("a", { a: { say: "a", next: [{ to: "b" }] }, b: { say: "b" } });
        expect(linksOf(list)["a"]).toEqual({ next: "b" });
    });
});
