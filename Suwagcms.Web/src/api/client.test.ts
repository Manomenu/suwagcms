import { describe, expect, it } from "vitest";

import { toError } from "./client";

describe("toError", () => {
    it("carries the server's detail", async () => {
        const response = new Response(JSON.stringify({ detail: "Send a non-empty text." }), { status: 422 });

        expect((await toError(response)).message).toBe("Send a non-empty text.");
    });

    it("falls back to the status when the body says nothing", async () => {
        const response = new Response("oops", { status: 502, statusText: "Bad Gateway" });

        expect((await toError(response)).message).toBe("502 Bad Gateway");
    });
});

describe("toError with a refusal", () => {
    it("carries every mistake the server lists, with its fix", async () => {
        const mistakes = [
            { code: "bot.idInvalid", path: "id", message: "The id is missing.", fix: "Send an id." },
            { code: "bot.nameMissing", path: "name", message: "The name is blank.", fix: "Send a name." },
        ];
        const response = new Response(JSON.stringify({ detail: "2 mistakes.", mistakes }), { status: 422 });

        const error = await toError(response);

        expect(error.mistakes.map((mistake) => mistake.fix)).toEqual(["Send an id.", "Send a name."]);
    });
});
