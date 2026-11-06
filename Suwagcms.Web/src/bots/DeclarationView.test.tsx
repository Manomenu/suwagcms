import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import type { DeclarationStatus } from "@/api/declaration";
import { DeclarationView } from "./DeclarationView";

const declared: DeclarationStatus = {
    bot: "rzeczoznawca",
    address: "http://rzeczoznawca.internal:8080",
    ask: "Answered",
    stored: "Declared",
    declaration: {
        bot: "rzeczoznawca",
        declaresNothing: false,
        formatVersion: 2,
        blocks: ["listen", "say"],
        codeActions: [{ id: "lookupPolicy", description: "Finds a policy", parameters: [], results: [] }],
    },
    answeredAt: "2026-11-04T21:40:00+00:00",
    reason: null,
    mistakes: [],
    formatFix: null,
    cannotRun: [],
};

const never: DeclarationStatus = { ...declared, stored: "NeverAnswered", declaration: null, answeredAt: null };

function html(status: DeclarationStatus): string {
    return renderToStaticMarkup(<DeclarationView status={status} asking={false} failure={null} onAsk={() => undefined} />);
}

describe("DeclarationView", () => {
    it("AC-bot-persona-13: says when the bot requires a persona, and says nothing when it does not", () => {
        const requiring: DeclarationStatus = {
            ...declared,
            declaration: { bot: "rzeczoznawca", declaresNothing: false, formatVersion: 2, blocks: ["say"], persona: "required" },
        };

        expect(html(requiring)).toContain("a version without one is refused");
        expect(html(declared)).not.toContain("a version without one is refused");
    });

    it("AC-bot-persona-15: says when the bot needs every part of a persona", () => {
        const requiring: DeclarationStatus = {
            ...declared,
            declaration: { bot: "rzeczoznawca", declaresNothing: false, formatVersion: 2, blocks: ["say"], persona: "complete" },
        };

        expect(html(requiring)).toContain("a version with any part left empty is refused");
        expect(html(declared)).not.toContain("a version with any part left empty is refused");
    });

    it("AC-bot-declaration-4: shows the format version, the blocks and the code actions, asked just now, with a button to ask again", () => {
        const page = html(declared);

        expect(page).toContain("Asked just now");
        expect(page).toContain("Ask again");
        expect(page).toContain("Format version");
        expect(page).toContain("listen");
        expect(page).toContain("say");
        expect(page).toContain("lookupPolicy");
    });

    it("AC-bot-declaration-5: a bot that has never answered shows that it has not", () => {
        const page = html({ ...never, ask: "Unreachable", reason: "cannot reach http://rzeczoznawca.internal:8080" });

        expect(page).toContain("Not asked yet");
        expect(page).toContain("cannot reach http://rzeczoznawca.internal:8080, and it has never answered");
    });

    it("AC-bot-declaration-6: an unreachable bot keeps the last answer, shown with its time and the reason", () => {
        const page = html({ ...declared, ask: "Unreachable", reason: "cannot reach http://rzeczoznawca.internal:8080" });

        expect(page).toContain("Last answer, from 2026-11-04 21:40 UTC");
        expect(page).toContain("cannot reach http://rzeczoznawca.internal:8080");
        expect(page).toContain("lookupPolicy");
    });

    it("AC-bot-declaration-7: a refused answer shows every mistake, and what the CMS holds stays", () => {
        const mistake = {
            stage: "Version" as const,
            resource: { kind: "Definition" as const, id: "" },
            path: "blocks",
            related: [],
        };
        const page = html({
            ...declared,
            ask: "Invalid",
            mistakes: [
                { ...mistake, code: "declaration.bot", message: "The address answers as bot 'x'.", fix: "Register the right address." },
                { ...mistake, code: "declaration.blocks.missing", message: "The bot declares no blocks.", fix: "State them." },
            ],
        });

        expect(page).toContain("The address answers as bot");
        expect(page).toContain("The bot declares no blocks.");
        expect(page).toContain("lookupPolicy");
    });

    it("AC-bot-declaration-8: a format version the CMS does not read is shown with its fix", () => {
        const page = html({
            ...declared,
            declaration: { ...declared.declaration, formatVersion: 3 },
            formatFix: "The bot wants format 3 and this CMS reads format 3: update Suwagcms.Sdk on the CMS.",
        });

        expect(page).toContain("The bot wants format 3");
    });

    it("AC-bot-declaration-10: a bot that declares nothing says so, and shows no declaration", () => {
        const page = html({ ...declared, stored: "DeclaresNothing", declaration: null });

        expect(page).toContain("This bot declares nothing");
        expect(page).not.toContain("Format version");
    });

    it("AC-bot-declaration-18: lists the procedures using a block the bot does not declare", () => {
        const page = html({
            ...declared,
            cannotRun: [
                {
                    code: "declaration.block",
                    stage: "Version",
                    resource: { kind: "Procedure", id: "orderValuation" },
                    path: "steps.ask.collect",
                    message: "Step 'ask' of procedure 'orderValuation' uses block 'collect', which this bot does not declare.",
                    fix: "Use another.",
                    related: [],
                },
            ],
        });

        expect(page).toContain("Procedures using a block the bot does not declare");
        expect(page).toContain("uses block &#x27;collect&#x27;");
    });
});
