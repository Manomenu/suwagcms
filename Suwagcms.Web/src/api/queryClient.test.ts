import { ApiError } from "@maniumek/suwfrontkit";
import { describe, expect, it } from "vitest";

import { retriesOnlyWhatMayPass } from "./queryClient";

describe("retries of a read", () => {
    it("AC-web-shell-9: an unknown id says it is not found at once: a refusal is never asked again, only a failure of the server or the network, twice", () => {
        expect(retriesOnlyWhatMayPass(0, new ApiError(404, { title: "Not Found" }))).toBe(false);
        expect(retriesOnlyWhatMayPass(0, new ApiError(422, {}))).toBe(false);
        expect(retriesOnlyWhatMayPass(0, new ApiError(503, {}))).toBe(true);
        expect(retriesOnlyWhatMayPass(1, new TypeError("Failed to fetch"))).toBe(true);
        expect(retriesOnlyWhatMayPass(2, new ApiError(503, {}))).toBe(false);
    });
});
