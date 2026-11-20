import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { mistakesOf, refusalOf } from "./client";

describe("a refusal", () => {
    it("AC-global-facts-6: carries every mistake the server lists, with its fix", () => {
        const mistakes = [
            { code: "bot.idInvalid", path: "id", message: "The id is missing.", fix: "Send an id." },
            { code: "bot.nameMissing", path: "name", message: "The name is blank.", fix: "Send a name." },
        ];

        const error = new ApiError(422, { detail: "2 mistakes.", mistakes });

        expect(error.message).toBe("2 mistakes.");
        expect(mistakesOf(error).map((mistake) => mistake.fix)).toEqual(["Send an id.", "Send a name."]);
    });

    it("AC-global-facts-6: a failure that is not the server's refusal has no mistakes and keeps its message", () => {
        const refusal = refusalOf(new TypeError("Failed to fetch"));

        expect(refusal.message).toBe("Failed to fetch");
        expect(mistakesOf(refusal)).toEqual([]);
        expect(mistakesOf(null)).toEqual([]);
    });
});
