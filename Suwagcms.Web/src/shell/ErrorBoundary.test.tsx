// @vitest-environment jsdom
import { act, useState } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Router } from "wouter";
import { memoryLocation } from "wouter/memory-location";

import { ErrorBoundary } from "./ErrorBoundary";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

let mounts = 0;
function Screen({ ok }: { ok: boolean }) {
    const [mountNumber] = useState(() => ++mounts);
    if (!ok) throw new Error("steps is not iterable");
    return <p>screen {mountNumber}</p>;
}

afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
});

describe("ErrorBoundary", () => {
    it("AC-web-shell-17: a screen that fails shows the error with Reload and a way back, the panel stays, and another address clears it", () => {
        vi.spyOn(console, "error").mockImplementation(() => undefined);
        const location = memoryLocation({ path: "/bots/rzeczoznawca/facts" });
        const container = document.createElement("div");
        document.body.append(container);
        const root = createRoot(container);
        const view = (ok: boolean) => (
            <Router hook={location.hook}>
                <nav>panel</nav>
                <ErrorBoundary>
                    <Screen ok={ok} />
                </ErrorBoundary>
            </Router>
        );
        act(() => {
            root.render(view(false));
        });

        expect(container.textContent).toContain("panel");
        expect(container.textContent).toContain("This screen failed to open.");
        expect(container.textContent).toContain("steps is not iterable");
        const links = [...container.querySelectorAll("a")];
        expect(links.map((link) => link.textContent)).toEqual(["Reload", "← Back to the bots"]);
        expect(links[1]?.getAttribute("href")).toBe("/bots");

        act(() => {
            location.navigate("/bots/rzeczoznawca/articles");
            root.render(view(true));
        });
        const shown = container.textContent;
        expect(shown).toMatch(/^panelscreen \d+$/);

        act(() => {
            location.navigate("/bots/rzeczoznawca/articles/new");
        });
        expect(container.textContent).toBe(shown);
        act(() => {
            root.unmount();
        });
    });
});
