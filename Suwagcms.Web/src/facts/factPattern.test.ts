import { describe, expect, it } from "vitest";

import type { Mistake } from "@/api/client";

import { NO_PATTERN, patternOf } from "@/api/textPattern";

import type { Fact } from "./api";
import { bodyOf, constraintOf, emptyForm, fieldOf, formOf } from "./factForm";

const coded: Fact = {
    name: "order.code",
    type: "text",
    description: "The code",
    writableBy: "set",
    values: null,
    min: null,
    max: null,
    maxLength: 10,
    pattern: { regex: "[A-Z]{2}", means: "two capitals", example: "AB", tellCustomer: "afterRefusal" },
    revision: 1,
    usedBy: [],
};

function mistake(path: string): Mistake {
    return { code: "fact.pattern.regexInvalid", stage: "Save", path, message: "m", fix: "f", related: [] } as unknown as Mistake;
}

describe("a text fact's pattern in the form", () => {
    it("AC-fact-pattern-10: opens with what the fact holds and saves it back unchanged", () => {
        expect(bodyOf(formOf(coded)).pattern).toEqual(coded.pattern);
    });

    it("AC-fact-pattern-10: a form with regex, means and example all empty sends no pattern, whatever the telling says", () => {
        expect(bodyOf(emptyForm()).pattern).toBeNull();
        expect(patternOf({ ...NO_PATTERN, patternTellCustomer: "afterRefusal" })).toBeNull();
        expect(bodyOf({ ...formOf(coded), patternRegex: " ", patternMeans: "", patternExample: "" }).pattern).toBeNull();
    });

    it("AC-fact-pattern-10: a half-filled pattern is sent so the server can name what is missing, with no blank example", () => {
        expect(patternOf({ ...NO_PATTERN, patternMeans: "words" })).toEqual({ regex: "", means: "words", tellCustomer: "upfront" });
    });

    it("AC-fact-pattern-10: a fact of another type sends none", () => {
        expect(bodyOf({ ...formOf(coded), type: "int" }).pattern).toBeNull();
    });

    it("AC-fact-pattern-10: each mistake of a pattern belongs beside its field, and the list names the pattern", () => {
        expect(fieldOf(mistake("pattern.regex"))).toBe("pattern.regex");
        expect(fieldOf(mistake("pattern.means"))).toBe("pattern.means");
        expect(fieldOf(mistake("pattern.example"))).toBe("pattern.example");
        expect(fieldOf(mistake("pattern.tellCustomer"))).toBe("pattern.tellCustomer");
        expect(constraintOf(coded)).toBe("text, up to 10, pattern [A-Z]{2}");
    });
});
