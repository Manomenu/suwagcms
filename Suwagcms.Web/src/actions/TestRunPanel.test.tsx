// @vitest-environment jsdom
import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { ActionTestAnswer, ActionView } from "./api";
import { runAction } from "./api";
import { TestRunPanel } from "./TestRunPanel";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock("./api", async (importOriginal) => ({ ...(await importOriginal<typeof import("./api")>()), runAction: vi.fn() }));

const action = (implementation: unknown): ActionView =>
    ({ id: "lookup", description: "Looks up", implementation }) as unknown as ActionView;

function show(shown: ActionView): HTMLElement {
    const container = document.createElement("div");
    document.body.append(container);
    act(() => createRoot(container).render(<TestRunPanel bot="rzeczoznawca" action={shown} unsaved={false} />));
    return container;
}

async function run(container: HTMLElement): Promise<void> {
    const button = [...container.querySelectorAll("button")].find((candidate) => candidate.textContent === "Run");
    await act(async () => {
        button?.click();
        await Promise.resolve();
    });
}

afterEach(() => {
    document.body.replaceChildren();
    vi.mocked(runAction).mockReset();
});

describe("TestRunPanel", () => {
    it("AC-action-test-run-16: a test run of a code action asks first as for a non-GET action, and runs nothing until it is confirmed", async () => {
        const container = show(action("code"));

        await run(container);

        expect(document.body.querySelector('[role="alertdialog"]')?.textContent).toContain(
            "This will really call the bot's code for lookup.",
        );
        expect(runAction).not.toHaveBeenCalled();
    });

    it("AC-action-test-run-9: a body of more than 2000 characters is shown cut, with a note saying so", async () => {
        const answer: ActionTestAnswer = {
            outcome: "success",
            results: {},
            reason: null,
            status: 200,
            elapsedMs: 5,
            answerStart: "x".repeat(2000),
            answerCut: true,
        };
        vi.mocked(runAction).mockResolvedValue(answer);
        const container = show(action({ http: { method: "GET", url: "https://crm.internal/x" } }));

        await run(container);

        expect(container.textContent).toContain("Only the first 2000 characters of the answer are shown.");
    });

    it("AC-action-test-run-9: an answer that was not cut carries no note", async () => {
        vi.mocked(runAction).mockResolvedValue({
            outcome: "success",
            results: {},
            reason: null,
            status: 200,
            elapsedMs: 5,
            answerStart: "short",
            answerCut: false,
        });
        const container = show(action({ http: { method: "GET", url: "https://crm.internal/x" } }));

        await run(container);

        expect(container.textContent).toContain("short");
        expect(container.textContent).not.toContain("Only the first 2000");
    });
});
