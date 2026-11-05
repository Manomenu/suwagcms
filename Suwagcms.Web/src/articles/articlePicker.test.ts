import { describe, expect, it } from "vitest";

import type { LinkTarget } from "./articleLink";
import { linkTargets, matching, targetsFor } from "./articlePicker";

const targets: LinkTarget[] = [
    { id: "cennik", title: "Price list", knowledgeBase: "offer" },
    { id: "wycena", title: "Valuation", knowledgeBase: "faq" },
    { id: "priceRules", title: "Rules", knowledgeBase: "forValuers" },
];

describe("the articles the picker offers", () => {
    it("AC-article-links-2: it lists the articles of every knowledge base", () => {
        expect(matching(targets, "", "other").map((target) => target.knowledgeBase)).toEqual(["offer", "faq", "forValuers"]);
    });

    it("AC-article-links-2: it narrows by a part of the title or of the id, ignoring case", () => {
        expect(matching(targets, "PRICE", "other").map((target) => target.id)).toEqual(["cennik", "priceRules"]);
        expect(matching(targets, "wyc", "other").map((target) => target.id)).toEqual(["wycena"]);
        expect(matching(targets, "nothing", "other")).toEqual([]);
    });

    it("AC-article-links-2: it leaves out the article being edited", () => {
        expect(matching(targets, "", "wycena").map((target) => target.id)).toEqual(["cennik", "priceRules"]);
    });
});

describe("the procedures the picker offers", () => {
    const all = linkTargets(
        [{ id: "cennik", title: "Price list", knowledgeBase: "offer" }],
        [
            { id: "conversation", description: "The base" },
            { id: "orderValuation", description: "The customer orders a valuation" },
            { id: "cennik", description: "A procedure named like an article" },
        ],
    );

    it("AC-article-procedure-links-2: the guide lists articles and procedures marked by kind, and leaves out conversation", () => {
        expect(targetsFor("llmGuide", all).map((target) => [target.kind ?? "article", target.id])).toEqual([
            ["article", "cennik"],
            ["procedure", "orderValuation"],
            ["procedure", "cennik"],
        ]);
    });

    it("AC-article-procedure-links-2: content lists no procedure", () => {
        expect(targetsFor("content", all).map((target) => target.id)).toEqual(["cennik"]);
    });

    it("AC-article-procedure-links-2: it narrows by a procedure's id or description, and the article being edited does not hide a procedure", () => {
        expect(matching(all, "ORDER", "other").map((target) => target.id)).toEqual(["orderValuation"]);
        expect(matching(all, "customer orders", "other").map((target) => target.id)).toEqual(["orderValuation"]);
        expect(matching(all, "named like", "cennik").map((target) => [target.kind, target.id])).toEqual([["procedure", "cennik"]]);
    });
});
