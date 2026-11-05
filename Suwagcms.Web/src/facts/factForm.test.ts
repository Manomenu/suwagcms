import { describe, expect, it } from "vitest";

import type { Mistake } from "@/api/client";

import type { Fact } from "./api";
import { bodyOf, emptyForm, fieldOf, formOf } from "./factForm";

function mistake(code: string, path: string): Mistake {
    return { code, path, message: "m", fix: "f", stage: "Save", resource: { kind: "GlobalFact", id: "x" }, related: [] };
}

const kind: Fact = {
    name: "order.kind",
    type: "enum",
    description: "What",
    writableBy: "correct",
    values: ["house", "flat"],
    min: null,
    max: null,
    maxLength: null,
    revision: 4,
    usedBy: [],
};

describe("emptyForm", () => {
    it("AC-global-facts-2: a new fact starts at writableBy set", () => {
        expect(emptyForm().writableBy).toBe("set");
        expect(bodyOf({ ...emptyForm(), name: "a", description: "d", maxLength: "3" }).writableBy).toBe("set");
    });
});

describe("bodyOf", () => {
    it("AC-global-facts-1: sends the constraints of the chosen type only", () => {
        const form = { ...emptyForm(), type: "enum", values: "house, flat", min: "1", maxLength: "9" };

        expect(bodyOf(form)).toEqual({
            type: "enum",
            description: "",
            writableBy: "set",
            values: ["house", "flat"],
            min: null,
            max: null,
            maxLength: null,
        });
        expect(bodyOf({ ...form, type: "int", min: "1", max: " 40 " })).toMatchObject({ values: null, min: 1, max: 40, maxLength: null });
        expect(bodyOf({ ...form, type: "text", maxLength: "80" })).toMatchObject({ values: null, min: null, max: null, maxLength: 80 });
    });

    it("AC-global-facts-6: sends what was typed even when it is wrong, so the server's mistakes are the only ones", () => {
        expect(bodyOf({ ...emptyForm(), type: "enum", values: "house, , house" }).values).toEqual(["house", "", "house"]);
        expect(bodyOf({ ...emptyForm(), type: "text", maxLength: "" }).maxLength).toBeNull();
    });
});

describe("formOf", () => {
    it("AC-global-facts-9: opens an edit with the values of the fact as text", () => {
        expect(formOf(kind)).toMatchObject({ name: "order.kind", type: "enum", values: "house, flat", writableBy: "correct" });
    });
});

describe("fieldOf", () => {
    it("AC-global-facts-6: a mistake belongs beside the field its path names", () => {
        expect(fieldOf(mistake("fact.description.missing", "description"))).toBe("description");
        expect(fieldOf(mistake("fact.enum.valueBlank", "values[2]"))).toBe("values");
        expect(fieldOf(mistake("fact.int.boundMissing", "max"))).toBe("max");
    });

    it("AC-global-facts-4: a name already taken is shown beside the name", () => {
        expect(fieldOf(mistake("resource.exists", "revision"))).toBe("name");
    });

    it("a mistake no field owns has no field", () => {
        expect(fieldOf(mistake("resource.stale", "revision"))).toBeNull();
        expect(fieldOf(mistake("save.invalid", "$"))).toBeNull();
    });
});
