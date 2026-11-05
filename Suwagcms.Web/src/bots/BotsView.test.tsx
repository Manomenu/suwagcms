import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { Router } from "wouter";

import type { Bot } from "@/api/bots";
import { BotsView, profilePath } from "./BotsView";

const rzeczoznawca: Bot = { id: "rzeczoznawca", name: "Rzeczoznawca", address: "http://rzeczoznawca.internal:8080" };
const sklep: Bot = { id: "sklep", name: "Sklep", address: "http://sklep.internal:8080" };

function render(bots: Bot[], openId: string | null): string {
    return renderToStaticMarkup(
        <Router ssrPath="/bots">
            <BotsView bots={bots} openId={openId} onRegister={() => undefined} registerForm={null} />
        </Router>,
    );
}

describe("BotsView", () => {
    it("AC-bot-profiles-7: with no bot registered it says so and offers to register one", () => {
        const html = render([], null);

        expect(html).toContain("No bots yet.");
        expect(html).toContain("Register a bot");
        expect(html).not.toContain("<ul");
    });

    it("AC-bot-profiles-1: every registered bot is listed with its name and address", () => {
        const html = render([rzeczoznawca, sklep], null);

        expect(html).toContain("Rzeczoznawca");
        expect(html).toContain("http://rzeczoznawca.internal:8080");
        expect(html).toContain("Sklep");
        expect(html).toContain("http://sklep.internal:8080");
        expect(html).not.toContain("No bots yet.");
    });

    it("AC-bot-profiles-11: an address naming a bot that does not exist says so and links to the list of bots", () => {
        const html = render([rzeczoznawca, sklep], "rzeczoznawka");

        expect(html).toContain("No bot &#x27;rzeczoznawka&#x27;");
        expect(html).toMatch(/<a [^>]*href="\/bots"[^>]*>Bots<\/a>/);
        expect(html).not.toContain("http://sklep.internal:8080");
    });

    it("AC-web-shell-6: asks to choose a bot or register one when none is chosen", () => {
        const html = render([rzeczoznawca, sklep], null);

        expect(html).toContain("Choose a bot from the panel to work on it, or register a new one.");
        expect(html).toContain("Register a bot");
    });

    it("AC-bot-profiles-2: shows the open bot's name and address, and not the other bots", () => {
        const html = render([rzeczoznawca, sklep], "sklep");

        expect(html).toContain("<h1");
        expect(html).toContain("Sklep");
        expect(html).toContain("http://sklep.internal:8080");
        expect(html).not.toContain("Rzeczoznawca");
        expect(html).not.toContain("No bot");
    });
});

describe("profilePath", () => {
    it("AC-bot-profiles-2: the profile of a bot is at /bots/<id>", () => {
        expect(profilePath("rzeczoznawca")).toBe("/bots/rzeczoznawca");
    });
});
