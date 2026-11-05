import { describe, expect, it } from "vitest";

import { changedLines } from "./historyLines";

describe("changedLines", () => {
    it("AC-change-history-1: a field's old and new values show only the lines that differ", () => {
        expect(changedLines("Cennik\nWycena domu: 900 zł\nKoniec", "Cennik\nWycena domu: 1100 zł\nKoniec")).toEqual({
            removed: ["Wycena domu: 900 zł"],
            added: ["Wycena domu: 1100 zł"],
        });
    });

    it("AC-change-history-4: a delete has only what the field held last", () => {
        expect(changedLines("Stary cennik", null)).toEqual({ removed: ["Stary cennik"], added: [] });
    });

    it("AC-change-history-1: a value that is not text shows as JSON", () => {
        expect(changedLines(80, 99)).toEqual({ removed: ["80"], added: ["99"] });
    });
});
