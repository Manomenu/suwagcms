import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { createQueryClient } from "./queryClient";

function retryOfReads(): (failures: number, error: Error) => boolean {
    const retry = createQueryClient().getDefaultOptions().queries?.retry;
    if (typeof retry !== "function") throw new Error("the query client has no retry function");
    return retry;
}

describe("retries of a read", () => {
    it("AC-web-shell-9: an unknown id says it is not found at once: a refusal is never asked again, only a failure of the server or the network, twice", () => {
        const retries = retryOfReads();

        expect(retries(0, new ApiError(404, { title: "Not Found" }))).toBe(false);
        expect(retries(0, new ApiError(422, {}))).toBe(false);
        expect(retries(0, new ApiError(503, {}))).toBe(true);
        expect(retries(1, new TypeError("Failed to fetch"))).toBe(true);
        expect(retries(2, new ApiError(503, {}))).toBe(false);
    });
});
