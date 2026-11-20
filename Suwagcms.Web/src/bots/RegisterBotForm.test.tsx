// @vitest-environment jsdom
import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import { RegisterBotForm } from "./RegisterBotForm";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

afterEach(() => {
    document.body.replaceChildren();
    vi.unstubAllGlobals();
});

describe("RegisterBotForm", () => {
    it("AC-web-shell-18: while a registration is on its way, Register is disabled and a second click sends no second request", () => {
        const fetch = vi.fn(() => new Promise<Response>(() => undefined));
        vi.stubGlobal("fetch", fetch);
        const container = document.createElement("div");
        document.body.append(container);
        const root = createRoot(container);
        act(() => {
            root.render(<RegisterBotForm onRegistered={() => undefined} onCancel={() => undefined} />);
        });
        const register = [...container.querySelectorAll("button")].find((button) => button.textContent === "Register");
        if (register === undefined) throw new Error("no Register button");

        act(() => {
            register.click();
        });
        act(() => {
            register.click();
        });

        expect(register.disabled).toBe(true);
        expect(fetch).toHaveBeenCalledTimes(1);
        act(() => {
            root.unmount();
        });
    });
});
