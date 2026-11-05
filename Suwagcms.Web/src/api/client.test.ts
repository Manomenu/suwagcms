import { describe, expect, it } from "vitest";

import { toError } from "./client";

describe("toError with a refusal", () => {
    it("AC-global-facts-6: carries every mistake the server lists, with its fix", async () => {
        const mistakes = [
            { code: "bot.idInvalid", path: "id", message: "The id is missing.", fix: "Send an id." },
            { code: "bot.nameMissing", path: "name", message: "The name is blank.", fix: "Send a name." },
        ];
        const response = new Response(JSON.stringify({ detail: "2 mistakes.", mistakes }), { status: 422 });

        const error = await toError(response);

        expect(error.mistakes.map((mistake) => mistake.fix)).toEqual(["Send an id.", "Send a name."]);
    });
});
