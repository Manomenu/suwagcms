import { describe, expect, it } from "vitest";

import { addStep, connect, type Draft } from "./draft";
import { arrange, nodeSize, placed } from "./layout";

function overlapping(draft: Draft, at: Record<string, { x: number; y: number }>): string[] {
    const names = Object.keys(draft.steps);
    return names.flatMap((a, index) =>
        names.slice(index + 1).flatMap((b) => {
            const [pa, pb] = [at[a], at[b]];
            if (pa === undefined || pb === undefined) return [`${a}/${b} has no position`];
            const [sa, sb] = [nodeSize(draft.steps[a] ?? {}), nodeSize(draft.steps[b] ?? {})];
            const apart = pa.x + sa.width <= pb.x || pb.x + sb.width <= pa.x || pa.y + sa.height <= pb.y || pb.y + sb.height <= pa.y;
            return apart ? [] : [`${a}/${b}`];
        }),
    );
}

function graph(): Draft {
    let draft: Draft = { description: "d", firstStep: "listen", whenStalled: null, steps: {} };
    for (const block of ["listen", "collect", "say", "assign", "say", "say"] as const) draft = addStep(draft, block).draft;
    draft = connect(draft, "listen", "next", "collect");
    draft = connect(draft, "collect", "next", "say");
    draft = connect(draft, "say", "next", "assign");
    draft = connect(draft, "assign", "next", "say2");
    draft = connect(draft, "say2", "next", "listen");
    return draft;
}

describe("the layout", () => {
    it("AC-procedure-creator-2: arranges a graph so that no two nodes overlap, loops included", () => {
        const draft = graph();

        expect(overlapping(draft, arrange(draft))).toEqual([]);
    });

    it("AC-procedure-creator-13: the arrange button gives every step a new position and a step with none is placed automatically", () => {
        const draft = graph();
        const stored = { listen: { x: 5000, y: 5000 }, collect: { x: 0, y: 0 } };

        const shown = placed(draft, stored);
        const all = arrange(draft);

        expect(shown["listen"]).toEqual({ x: 5000, y: 5000 });
        expect(shown["say"]).toEqual(all["say"]);
        expect(Object.keys(shown).sort()).toEqual(Object.keys(draft.steps).sort());
        expect(all["listen"]).not.toEqual({ x: 5000, y: 5000 });
        expect(Object.values(all).every((at) => Number.isFinite(at.x) && Number.isFinite(at.y))).toBe(true);
    });

    it("AC-procedure-creator-13: a step with no position never lands on one that has, even after steps were added", () => {
        const draft = graph();
        const all = arrange(draft);
        const stored = { listen: all["say"] ?? { x: 0, y: 0 }, collect: all["collect"] ?? { x: 0, y: 0 } };

        const shown = placed(draft, stored);

        expect(overlapping(draft, shown)).toEqual([]);
        expect(Object.keys(shown).sort()).toEqual(Object.keys(draft.steps).sort());
    });

    it("AC-procedure-creator-13: the same draft is always arranged the same way", () => {
        expect(arrange(graph())).toEqual(arrange(graph()));
    });
});
