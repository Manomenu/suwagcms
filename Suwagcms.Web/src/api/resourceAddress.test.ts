import { describe, expect, it } from "vitest";

import { addressOf, editHref, listHref, newHref, withListSearch } from "./resourceAddress";

describe("the address of a resource", () => {
    it("AC-web-shell-7: the list, new and edit have one form on every collection", () => {
        for (const collection of ["facts", "knowledge-bases", "articles", "procedures", "actions"] as const) {
            expect(listHref("sklep", collection)).toBe(`/bots/sklep/${collection}`);
            expect(newHref("sklep", collection)).toBe(`/bots/sklep/${collection}/new`);
            expect(editHref("sklep", collection, "offer")).toBe(`/bots/sklep/${collection}/offer/edit`);
            expect(addressOf(listHref("sklep", collection), collection)).toEqual({ screen: "list" });
            expect(addressOf(newHref("sklep", collection), collection)).toEqual({ screen: "new" });
            expect(addressOf(editHref("sklep", collection, "offer"), collection)).toEqual({ screen: "edit", id: "offer" });
        }
    });

    it("AC-web-shell-8: an id with dots or characters to encode reads back the same, and 'new' can be an id", () => {
        for (const id of ["customer.email", "a b/c", "new"]) {
            expect(addressOf(editHref("sklep", "facts", id), "facts")).toEqual({ screen: "edit", id });
        }
    });

    it("AC-web-shell-8: a malformed % names that id instead of breaking the page", () => {
        expect(addressOf("/bots/sklep/facts/%E0%A4%A/edit", "facts")).toEqual({ screen: "edit", id: "%E0%A4%A" });
    });

    it("AC-web-shell-7: anything else under the collection is its list", () => {
        expect(addressOf("/bots/sklep/facts/offer", "facts")).toEqual({ screen: "list" });
        expect(addressOf("/bots/sklep/facts/offer/edit/more", "facts")).toEqual({ screen: "list" });
        expect(addressOf("/bots/sklep/facts/new/more", "facts")).toEqual({ screen: "list" });
    });
});

describe("withListSearch", () => {
    it("AC-pagination-12: the list's page and filters travel, and what belongs to one edit screen stays behind", () => {
        expect(withListSearch("/bots/b/facts", "")).toBe("/bots/b/facts");
        expect(withListSearch("/bots/b/facts", "page=3&language=pl")).toBe("/bots/b/facts?page=3&language=pl");
        expect(withListSearch("/bots/b/facts", "page=3&historyPage=2")).toBe("/bots/b/facts?page=3");
        expect(withListSearch("/bots/b/procedures", "page=3&if=ask:0&if=ask:1")).toBe("/bots/b/procedures?page=3");
        expect(withListSearch("/bots/b/procedures", "step=hello&historyPage=2")).toBe("/bots/b/procedures");
    });

    it("AC-change-history-24: leaving a version's page takes the list's page and leaves its panel's page and comparison behind", () => {
        expect(withListSearch("/bots/b/versions", "page=2&historyPage=3&compare=12")).toBe("/bots/b/versions?page=2");
    });
});
