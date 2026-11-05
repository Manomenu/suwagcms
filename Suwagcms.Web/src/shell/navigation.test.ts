import { describe, expect, it } from "vitest";

import { botHref, navigationOf, TABS, tabHref } from "./navigation";

describe("the Persona tab", () => {
    it("AC-bot-persona-1: goes between Actions and Versions", () => {
        const ids: string[] = TABS.map((tab) => tab.id);

        expect(ids.slice(ids.indexOf("actions"), ids.indexOf("versions") + 1)).toEqual(["actions", "persona", "versions"]);
    });
});

describe("navigationOf", () => {
    it("AC-web-shell-4: has no bot and no tab on / and /bots", () => {
        for (const path of ["/", "/bots", "/bots/"]) {
            expect(navigationOf(path)).toEqual({ bot: null, tab: null, fills: false });
        }
    });

    it("AC-web-shell-4: has the bot and no tab on the profile", () => {
        expect(navigationOf("/bots/sklep")).toEqual({ bot: "sklep", tab: null, fills: false });
    });

    it("AC-web-shell-4: has each of the tabs as the current tab", () => {
        for (const { id } of TABS) {
            expect(navigationOf(`/bots/sklep/${id}`)).toEqual({ bot: "sklep", tab: id, fills: false });
        }
    });

    it("AC-web-shell-4: lights procedures and fills the content on the creator", () => {
        expect(navigationOf("/bots/sklep/procedures/refund")).toEqual({ bot: "sklep", tab: "procedures", fills: true });
    });

    it("AC-web-shell-4: decodes the id, and a malformed % does not throw", () => {
        expect(navigationOf("/bots/a%20b").bot).toBe("a b");
        expect(() => navigationOf("/bots/100%/facts")).not.toThrow();
        expect(navigationOf("/bots/100%/facts")).toEqual({ bot: "100%", tab: "facts", fills: false });
    });

    it("AC-web-shell-4: builds hrefs that encode the id", () => {
        expect(botHref("a b")).toBe("/bots/a%20b");
        expect(tabHref("a b", "facts")).toBe("/bots/a%20b/facts");
    });
});
