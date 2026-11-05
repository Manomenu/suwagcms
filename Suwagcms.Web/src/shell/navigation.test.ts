import { describe, expect, it } from "vitest";

import { botHref, navigationOf, TABS, tabHref } from "./navigation";

describe("navigationOf", () => {
    it("has no bot and no tab on / and /bots", () => {
        for (const path of ["/", "/bots", "/bots/"]) {
            expect(navigationOf(path)).toEqual({ bot: null, tab: null, fills: false });
        }
    });

    it("has the bot and no tab on the profile", () => {
        expect(navigationOf("/bots/sklep")).toEqual({ bot: "sklep", tab: null, fills: false });
    });

    it("has each of the five tabs as the current tab", () => {
        for (const { id } of TABS) {
            expect(navigationOf(`/bots/sklep/${id}`)).toEqual({ bot: "sklep", tab: id, fills: false });
        }
    });

    it("lights procedures and fills the content on the creator", () => {
        expect(navigationOf("/bots/sklep/procedures/refund")).toEqual({ bot: "sklep", tab: "procedures", fills: true });
    });

    it("decodes the id, and a malformed % does not throw", () => {
        expect(navigationOf("/bots/a%20b").bot).toBe("a b");
        expect(() => navigationOf("/bots/100%/facts")).not.toThrow();
        expect(navigationOf("/bots/100%/facts")).toEqual({ bot: "100%", tab: "facts", fills: false });
    });

    it("builds hrefs that encode the id", () => {
        expect(botHref("a b")).toBe("/bots/a%20b");
        expect(tabHref("a b", "facts")).toBe("/bots/a%20b/facts");
    });
});
