import { describe, expect, it } from "vitest";

import type { DeclarationStatus } from "@/api/declaration";
import { BLOCKS } from "./draft";
import { offeredBlocks } from "./offeredBlocks";

const base: DeclarationStatus = {
    bot: "rzeczoznawca",
    address: "http://rzeczoznawca.internal:8080",
    ask: "NotAsked",
    stored: "Declared",
    declaration: { bot: "rzeczoznawca", declaresNothing: false, formatVersion: 2, blocks: ["listen", "say"], codeActions: [] },
    answeredAt: "2026-11-04T21:40:00+00:00",
    reason: null,
    mistakes: [],
    formatFix: null,
    cannotRun: [],
};

describe("offeredBlocks", () => {
    it("AC-procedure-creator-19: offers only the blocks the bot declares", () => {
        expect(offeredBlocks(base)).toEqual({ blocks: ["listen", "say"], note: null, knowledge: false });
    });

    it("AC-procedure-creator-19: offers every block and says nothing is being checked when the bot has declared none", () => {
        for (const status of [
            { ...base, stored: "NeverAnswered", declaration: null, answeredAt: null },
            { ...base, stored: "DeclaresNothing", declaration: null },
        ] satisfies DeclarationStatus[]) {
            const offered = offeredBlocks(status);
            expect(offered.blocks).toEqual(BLOCKS);
            expect(offered.note).toContain("nothing is being checked");
        }
    });

    it("AC-procedure-creator-38: the section shows when the declaration says so or answers nothing, and hides when it answers without it", () => {
        expect(offeredBlocks(base).knowledge).toBe(false);
        const declaration = base.declaration;
        if (declaration === null) throw new Error("the base status holds a declaration");
        expect(offeredBlocks({ ...base, declaration: { ...declaration, knowledge: true } }).knowledge).toBe(true);
        expect(offeredBlocks({ ...base, stored: "DeclaresNothing", declaration: null }).knowledge).toBe(true);
        expect(offeredBlocks({ ...base, formatFix: "Update the SDK." }).knowledge).toBe(true);
    });

    it("AC-procedure-creator-19: offers every block when the bot wants a format the CMS does not read", () => {
        expect(offeredBlocks({ ...base, formatFix: "Update the SDK." }).blocks).toEqual(BLOCKS);
    });
});
