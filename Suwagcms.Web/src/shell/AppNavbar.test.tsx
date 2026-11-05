import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Bot } from "@/api/bots";

import { AppNavbar } from "./AppNavbar";
import { TABS, type Tab } from "./navigation";

const rzeczoznawca: Bot = { id: "rzeczoznawca", name: "Rzeczoznawca", address: "http://rzeczoznawca.internal:8080" };
const sklep: Bot = { id: "sklep", name: "Sklep", address: "http://sklep.internal:8080" };

function render(bot: string | null, tab: Tab | null): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots">
            <AppNavbar bots={[rzeczoznawca, sklep]} failure={null} bot={bot} tab={tab} />
        </Router>,
    );
}

describe("AppNavbar", () => {
    it("AC-web-shell-1: the bot is chosen first: with no bot open the bots are listed and there is no tab link", () => {
        const html = render(null, null);

        expect(html).toContain("Rzeczoznawca");
        expect(html).toContain("Sklep");
        expect(html).toContain('href="/bots/sklep"');
        for (const { id } of TABS) expect(html).not.toContain(`/${id}"`);
    });

    it("AC-web-shell-2: tabs appear only under the open bot", () => {
        const html = render("sklep", null);

        for (const { id } of TABS) {
            expect(html).toContain(`href="/bots/sklep/${id}"`);
            expect(html).not.toContain(`/bots/rzeczoznawca/${id}`);
        }
    });

    it("AC-web-shell-2: an id that is not in the list has no tabs", () => {
        const html = render("nie-ma", null);

        for (const { id } of TABS) expect(html).not.toContain(`/${id}"`);
    });

    it("AC-web-shell-3: marks the open bot as the current page, and as current while one of its tabs is open", () => {
        expect(render("sklep", null)).toMatch(/<li aria-current="page"><a [^>]*>Sklep/);
        expect(render("sklep", "facts")).toMatch(/<li aria-current="true"><a [^>]*>Sklep/);
    });

    it("AC-web-shell-3: marks only the current tab", () => {
        const html = render("sklep", "versions");

        expect(html.match(/aria-current="page"/g)).toHaveLength(1);
        expect(html).toMatch(/aria-current="page"[^>]*>Versions/);
    });
});
