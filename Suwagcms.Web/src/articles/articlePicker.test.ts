import { describe, expect, it } from "vitest";

import type { LinkTarget } from "./articleLink";
import { matching } from "./articlePicker";

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
