import { describe, expect, it } from "vitest";

import { addressOf, editHref, listHref, newHref } from "./resourceAddress";

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
