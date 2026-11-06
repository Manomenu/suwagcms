import { describe, expect, it } from "vitest";

import { lastPage, pageAsked, rowsOfPage } from "./pagination";

describe("the rules of a page", () => {
    const rows = Array.from({ length: 45 }, (_, index) => index + 1);

    it("AC-pagination-4: page 2 of 45 rows at 20 a page is rows 21 to 40, of 3 pages", () => {
        expect(lastPage(rows.length, 20)).toBe(3);
        expect(rowsOfPage(rows, 2, 20)).toEqual(rows.slice(20, 40));
        expect(rowsOfPage(rows, 3, 20)).toEqual([41, 42, 43, 44, 45]);
    });

    it("AC-pagination-4: a missing or invalid page is the first", () => {
        for (const raw of [null, "", "0", "-1", "abc", "1.5", "01", "99999999999999"]) expect(pageAsked(raw)).toBe(1);
        expect(pageAsked("99")).toBe(99);
    });

    it("AC-pagination-4: no rows are one page", () => {
        expect(lastPage(0, 20)).toBe(1);
        expect(lastPage(20, 20)).toBe(1);
        expect(lastPage(21, 20)).toBe(2);
    });
});
