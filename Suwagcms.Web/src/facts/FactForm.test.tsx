// @vitest-environment jsdom
import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { emptyForm } from "./factForm";
import { FactForm } from "./FactForm";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

afterEach(() => {
    document.body.replaceChildren();
    vi.unstubAllGlobals();
});

describe("FactForm", () => {
    it("AC-web-shell-18: while a save is on its way, Save is disabled and a second click sends no second request", () => {
        const fetch = vi.fn(() => new Promise<Response>(() => undefined));
        vi.stubGlobal("fetch", fetch);
        const container = document.createElement("div");
        document.body.append(container);
        const root = createRoot(container);
        act(() => {
            root.render(
                <FactForm
                    bot="rzeczoznawca"
                    initial={{ ...emptyForm(), name: "customer.city" }}
                    editing
                    revision={3}
                    onSaved={() => undefined}
                    onReload={() => undefined}
                    onCancel={() => undefined}
                />,
            );
        });
        const save = [...container.querySelectorAll("button")].find((button) => button.textContent === "Save");
        if (save === undefined) throw new Error("no Save button");

        act(() => {
            save.click();
        });
        act(() => {
            save.click();
        });

        expect(save.disabled).toBe(true);
        expect(fetch).toHaveBeenCalledTimes(1);
        act(() => {
            root.unmount();
        });
    });
});
