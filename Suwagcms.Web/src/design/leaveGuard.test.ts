// @vitest-environment jsdom
import { createElement } from "react";
import { createRoot } from "react-dom/client";
import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { mayLeave, useLeaveGuard } from "./leaveGuard";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

function Guarded({ message }: { message: string }) {
    useLeaveGuard(true, message);
    return null;
}

afterEach(() => vi.restoreAllMocks());

describe("leave guard", () => {
    it("AC-articles-13: asks nothing when no page is guarded", () => {
        const confirm = vi.spyOn(window, "confirm");
        expect(mayLeave()).toBe(true);
        expect(confirm).not.toHaveBeenCalled();
    });

    it("AC-articles-13, AC-procedure-creator-17: asks the question while guarded and returns the answer, then asks nothing after unmount", () => {
        const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
        const root = createRoot(document.createElement("div"));
        act(() => root.render(createElement(Guarded, { message: "Discard changes?" })));
        expect(mayLeave()).toBe(false);
        expect(confirm).toHaveBeenCalledWith("Discard changes?");
        confirm.mockReturnValue(true);
        expect(mayLeave()).toBe(true);
        act(() => root.unmount());
        confirm.mockClear();
        expect(mayLeave()).toBe(true);
        expect(confirm).not.toHaveBeenCalled();
    });
});
